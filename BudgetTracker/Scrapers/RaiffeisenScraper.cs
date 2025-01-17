﻿using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Xml.Linq;
using System.Xml.XPath;
using BudgetTracker.Model;
using JetBrains.Annotations;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Logging;
using OpenQA.Selenium;

namespace BudgetTracker.Scrapers
{
    [UsedImplicitly]
    internal class RaiffeisenScraper : GenericScraper
    {
        public RaiffeisenScraper(ObjectRepository repository, ILoggerFactory logger) : base(repository, logger)
        {
        }

        public override string ProviderName => "Райффайзен";

        public override IList<MoneyStateModel> Scrape(ScraperConfigurationModel configuration, Chrome chrome)
        {
            var driver = chrome.Driver;
            Login(configuration, chrome);

            driver.Navigate().GoToUrl(@"https://online.raiffeisen.ru/#/accounts");
            
            var accounts = GetElements(driver, By.TagName("account-widget"));

            var result = new List<MoneyStateModel>();
            
            foreach (var acc in accounts)
            {
                try
                {
                    var titleElement = acc.FindElement(By.ClassName("product-header-title__name-text"));
                    var text = titleElement.GetAttribute("textContent");

                    var amountWait = acc.FindElement(By.ClassName("product-header-info__value"));

                    var amount = amountWait.GetAttribute("textContent");
                    var amountClear = new string(amount.Where(v => char.IsDigit(v) || v == ',').ToArray());

                    var amountNumber = double.Parse(amountClear, new NumberFormatInfo()
                    {
                        NumberDecimalSeparator = ","
                    });

                    var ccySign = acc.FindElement(By.ClassName("amount__symbol"));
                    var ccyText = ccySign.GetAttribute("textContent");

                    result.Add(Money(text, amountNumber, ccyText));
                }
                catch (Exception ex)
                {
                    Logger.LogError("Failed to parse row, continue", ex);
                } 
            }

            return result;
        }

        public override IList<PaymentModel> ScrapeStatement(ScraperConfigurationModel configuration, Chrome chrome, DateTime startFrom)
        {
            var driver = chrome.Driver;
            Login(configuration, chrome);

            driver.Navigate().GoToUrl(@"https://online.raiffeisen.ru/#/history/statement");

            var accounts = GetElements(driver, By.TagName("c-select-option-account"));

            var link = GetElements(driver, By.TagName("a")).First(v => v.GetAttribute("href")?.Contains("/transaction.ofx?") == true);
            var linkText = link.GetAttribute("href");
            
            var build = new Uri(linkText);

            var result = new List<PaymentModel>();
                
            var urlFormat = @"https://online.raiffeisen.ru/rest/account/{accountId}/transaction.ofx?from={from}&to={to}&sort=date&order=desc&access_token={token}";

            var originalQuery = QueryHelpers.ParseQuery(build.Query);

            var accessToken = originalQuery["access_token"].First();
            
            var accountDetails = accounts.Select(v =>
            {
                var id = v.FindElement(By.TagName("div")).GetAttribute("data-account-id");
                var textElement = v.FindElement(By.TagName("account-logo")).FindElement(By.XPath(".."));
                var name = textElement.GetAttribute("textContent").Trim();
                return (id, name);
            }).Distinct().ToList();

            Logger.LogInformation($"Found {accountDetails.Count} Raiffeisen accounts");
            
            foreach (var account in accountDetails)
            {
                var accountId = account.id;
                var accountName = account.name;
                var url = urlFormat.Replace("{accountId}", accountId)
                    .Replace("{from}", startFrom.ToString("yyyy-MM-ddTHH:mm"))
                    .Replace("{to}", DateTime.Now.ToString("yyyy-MM-ddTHH:mm"))
                    .Replace("{token}", accessToken);
                
                // Raiffeisen sometimes doesn't return all payments on first call
                for (int i = 0; i < 3; i++)
                {
                    driver.Navigate().GoToUrl(url);

                    Logger.LogInformation($"Getting statement for {account.name} at {url}, attempt {i}");

                    int waited = 0;
                    while (chrome.GetDownloads().Count < 1 && waited < 300)
                    {
                        WaitForPageLoad(driver);
                        waited++;
                    }

                    Thread.Sleep(10000);

                    var files = chrome.GetDownloads();
                    if (files.Count == 1)
                    {
                        var ofxFile = files.First();

                        var doc = File.ReadAllText(ofxFile.FullName);

                        var xdoc = XDocument.Parse(doc);

                        var payments = ParseOfx(xdoc, accountName);

                        Logger.LogInformation($"Got {payments.Count} payments from {url}, attempt {i}");
                        result.AddRange(payments);
                        ofxFile.Delete();
                    }
                }
            }

            var oldCount = result.Count;
            result = result.GroupBy(v => v.StatementReference).Select(v => v.First()).ToList();
            Logger.LogInformation($"Deduplicated payments {oldCount} -> {result.Count}");

            return result;
        }
        
        private void Login(ScraperConfigurationModel configuration, Chrome chrome)
        {
            var driver = chrome.Driver;
            driver.Navigate().GoToUrl(@"https://online.raiffeisen.ru/");
            WaitForPageLoad(driver);

            var name = GetElement(driver, By.ClassName("login-form__username-wrap")).FindElement(By.TagName("input"));
            var pass = GetElement(driver, By.ClassName("login-form__password-wrap")).FindElement(By.TagName("input"));
            name.Click();
            chrome.SendKeys(configuration.Login);
            pass.Click();
            chrome.SendKeys(configuration.Password);
            
            var smsModel = WaitForSms(() =>
            {
                chrome.SendKeys(Keys.Return);
                WaitForPageLoad(driver);
            }, s => s.Message.ToLower().Contains("r-online"));

            WaitForPageLoad(chrome.Driver);
            
            var code = new string(smsModel.Message.Where(char.IsDigit).Take(4).ToArray());
            chrome.SendKeys(code);
            chrome.SendKeys(Keys.Return);
        }
    }
}