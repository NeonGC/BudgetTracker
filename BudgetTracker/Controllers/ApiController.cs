﻿using System;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Web;
using BudgetTracker.JsModel;
using BudgetTracker.JsModel.Attributes;
using BudgetTracker.Model;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace BudgetTracker.Controllers
{
    [HideFromRest]
    public class ApiController : Controller
    {
        private readonly ILogger<ApiController> _logger;
        private readonly ObjectRepository _objectRepository;

        public ApiController(ILogger<ApiController> logger, ObjectRepository objectRepository)
        {
            _logger = logger;
            _objectRepository = objectRepository;
        }

        private string GetJsonResponseForSms(bool success, string error = null)
        {
            return JsonConvert.SerializeObject(
                new
                {
                    payload = new
                    {
                        success,
                        error
                    }
                });
        }

        [Route("sms")]
        public async Task<IActionResult> SmsIfThisThenThat()
        {
            var success = GetJsonResponseForSms(true);

            try
            {
                var data = await new HttpRequestStreamReader(HttpContext.Request.Body, Encoding.UTF8).ReadToEndAsync();
                _logger.LogInformation(data);
                var items = JObject.Parse(data);
                var from = items["from"].Value<string>();

                if (string.IsNullOrWhiteSpace(from))
                    from = items["fromNumber"].Value<string>();

                var message = items["message"].Value<string>();
                var when = items["sent_timestamp"].Value<string>();
                var whenDate = DateTime.Parse(when.Replace("at", ""));

                var smsModel = new SmsModel(@from, message, whenDate);

                lock (typeof(SmsModel))
                {
                    var existingMessage = _objectRepository.Set<SmsModel>()
                        .FirstOrDefault(s => Math.Abs((s.When - smsModel.When).TotalMinutes) < 2  && smsModel.Message.StartsWith(s.Message));
                    if (existingMessage == null)
                    {
                        _objectRepository.Add(smsModel);
                    }
                    else
                    {
                        existingMessage.Message = smsModel.Message;
                    }
                }

                _logger.LogInformation($"parsed form: {from} {message} {when}");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to parse sms");

                success = GetJsonResponseForSms(false, "failed to get result " + ex);
            }

            return Content(success, "application/json");
        }

        [Route("sms-tasker")]
        public async Task<IActionResult> SmsTasker()
        {
            try
            {
                var data = await new HttpRequestStreamReader(HttpContext.Request.Body, Encoding.UTF8).ReadToEndAsync();
                data = HttpUtility.UrlDecode(data);
                if (data == null)
                {
                    throw new NullReferenceException("request is null on sms-tasker");
                }
                _logger.LogInformation(data);
                var d = data.Replace("\n", " ").Replace("\r", " ").Split("/=/=", 6).Select(v=>v.Trim()).ToList();
                var time = d[0];
                var date = d[1];
                var fromName = d[2];
                var from = d[3];
                var message = d[5];

                if (!string.IsNullOrWhiteSpace(fromName))
                    from = fromName;

                if (!DateTime.TryParseExact(date + " " + time, "M-d-y H.m", CultureInfo.CurrentCulture, DateTimeStyles.None, out var whenDate) &&
                    !DateTime.TryParseExact(date + " " + time, "d.M.yyyy H.m", CultureInfo.CurrentCulture, DateTimeStyles.None, out whenDate))
                    throw new InvalidOperationException("Failed to parse dateTime {" + date + " " + time + "}");

                var smsModel = new SmsModel(@from, message, whenDate);

                lock (typeof(SmsModel))
                {
                    var existingMessage = _objectRepository.Set<SmsModel>()
                        .FirstOrDefault(s => Math.Abs((s.When - smsModel.When).TotalMinutes) < 2 && s.Message.StartsWith(smsModel.Message));
                    if (existingMessage == null)
                    {
                        _objectRepository.Add(smsModel);
                    }
                    else
                    {
                        existingMessage.From = smsModel.From;
                    }
                }

                _logger.LogInformation($"parsed form: {from} {message} {whenDate}");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to parse sms");
            }

            return new EmptyResult();
        }

        [Route("post-data")]
        public IActionResult PostData([Bind] string name, [Bind] string value, [Bind] string ccy)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(name) || 
                    !double.TryParse(value.Replace(".", ",").Trim(), NumberStyles.Any, new NumberFormatInfo(){NumberDecimalSeparator = ","}, out var valueParsed) 
                    || double.IsNaN(valueParsed) 
                    || double.IsInfinity(valueParsed))
                {
                    _logger.LogError($"Failed to parse post-data: {name} / {value} / {ccy}");
                    return new ContentResult
                    {
                        Content = "Bad request",
                        StatusCode = 400
                    };
                }
                
                var existing = _objectRepository.Set<MoneyColumnMetadataModel>()
                    .FirstOrDefault(v => v.Provider == "API" && v.AccountName == name);

                if (existing == null)
                {
                    existing = new MoneyColumnMetadataModel("API", name)
                    {
                        UserFriendlyName = name
                    };
                    _objectRepository.Add(existing);
                }
                
                _objectRepository.Add(new MoneyStateModel
                {
                    Column = existing,
                    Ccy = ccy,
                    Amount = valueParsed,
                    When = DateTime.UtcNow
                });
                
                _logger.LogInformation($"Parsed post-data: {name} / {value} / {ccy}");
                return new ContentResult
                {
                    Content = "OK",
                    StatusCode = 200
                };
            }
            catch
            {
                _logger.LogError($"Failed to parse post-data: {name} / {value} / {ccy}");
                return new ContentResult
                {
                    Content = "ERROR",
                    StatusCode = 500
                };
            }
        }
    }
}