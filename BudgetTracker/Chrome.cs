using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Reflection;
using System.Text;
using System.Threading;
using Newtonsoft.Json;
using OpenQA.Selenium;
using OpenQA.Selenium.Chrome;
using OpenQA.Selenium.Interactions;
using OpenQA.Selenium.Remote;

namespace BudgetTracker
{
    public class Chrome : IDisposable
    {
        private readonly string _downloadDir;
        private WebDriver _driver;

        public Chrome()
        {
            _downloadDir = Path.Combine(Startup.ChromeDownloads, "ChromeWebDriverDownload");
            Directory.CreateDirectory(_downloadDir);
        }

        public bool HasDriver => _driver != null;

        public WebDriver Driver
        {
            get
            {
                if (_driver == null)
                {
                    lock (this)
                    {
                        if (_driver == null)
                        {
                            _driver = CreateDriver(_downloadDir);
                        }
                    }
                }

                return _driver;
            }
        }

        public void SendKeys(string strToSend) => new Actions(Driver).SendKeys(strToSend).Build().Perform();

        public void MoveToElement(IWebElement element, int x, int y) => new Actions(Driver).MoveToElement(element, x, y).Build().Perform();

        public IList<FileInfo> GetDownloads() => Directory.GetFiles(_downloadDir).Select(v => new FileInfo(v)).ToList();

        public void CleanupDownloads()
        {
            Directory.Delete(_downloadDir,true);
            Directory.CreateDirectory(_downloadDir);
        }

        public void Dispose()
        {
            Directory.Delete(_downloadDir, true);
            _driver?.Close();
            _driver?.Dispose();
        }

        public void Reset()
        {
            CleanupDownloads();
            
            var d = _driver;
            _driver = null;
            try
            {
                d?.Close();
            }
            catch (Exception ex)
            {
            }

            d?.Dispose();
        }

        private static WebDriver CreateDriver(string downloadDir)
        {
            var driver = new RemoteWebDriver(new Uri(Startup.ChromeDriverUrl), new ChromeOptions());
            var url = new Uri(new Uri(Startup.ChromeDriverUrl), "/session/" + driver.SessionId + "/chromium/send_command").AbsoluteUri;
            using (var httpClient = new HttpClient())
            {
                var content = new StringContent(JsonConvert.SerializeObject(new Dictionary<string, object>
                    {
                        {"cmd", "Page.setDownloadBehavior"},
                        {
                            "params", new Dictionary<string, string>
                            {
                                {"behavior", "allow"},
                                {"downloadPath", downloadDir}
                            }
                        }
                    }), Encoding.UTF8,
                    "application/json");
                httpClient.PostAsync(url, content).GetAwaiter().GetResult();
            }

            Thread.Sleep(30);
            driver.Manage().Timeouts().PageLoad = System.TimeSpan.FromSeconds(30);
            driver.Manage().Timeouts().ImplicitWait = TimeSpan.FromSeconds(3);
            return driver;
        }
    }
}