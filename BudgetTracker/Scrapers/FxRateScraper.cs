using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using BudgetTracker.Model;
using JetBrains.Annotations;
using Microsoft.Extensions.Logging;
using OpenQA.Selenium;
using OpenQA.Selenium.Chrome;

namespace BudgetTracker.Scrapers
{
    [UsedImplicitly]
    internal class FxRateScraper : GenericScraper
    {
        public FxRateScraper(ObjectRepository repository, ILoggerFactory factory) : base(repository, factory)
        {
        }

        public override string ProviderName => "FX";

        public override IList<MoneyStateModel> Scrape(ScraperConfigurationModel configuration, Chrome chrome)
        {
            var driver = chrome.Driver;

            var result = new List<MoneyStateModel>();

            driver.Navigate().GoToUrl("https://eodhistoricaldata.com/financial-summary/GSPC.INDX");
            WaitForPageLoad(driver);
            var moneyStateModel = ParseMoney("SP500", driver);
            result.Add(moneyStateModel);

            foreach (var item in CurrencyExtensions.KnownCurrencies.Where(v => v != CurrencyExtensions.RUB))
            {
                var url = $"https://eodhistoricaldata.com/financial-summary/{item.ToUpper()}RUB.FOREX";
                driver.Navigate().GoToUrl(url);
                Logger.LogInformation($"Scraping {url}");
                var itemRub = item + "/" + CurrencyExtensions.RUB;
                var msm = ParseMoney(itemRub, driver);
                result.Add(msm);
            }

            return result;
        }

        private MoneyStateModel ParseMoney(string account, WebDriver driver)
        {
            var rows = GetElements(driver, By.ClassName("grid4"));
            foreach (var r in rows)
            {
                var cells = r.FindElements(By.TagName("div"));
                string resultCell = null;
                if (cells[0].Text.Equals("Close"))
                {
                    resultCell = cells[1].Text;
                    if (string.IsNullOrWhiteSpace(resultCell))
                    {
                        resultCell = cells[1].GetAttribute("textContent");
                    }
                }

                if (cells[2].Text.Equals("Close"))
                {
                    resultCell = cells[3].Text;
                    if (string.IsNullOrWhiteSpace(resultCell))
                    {
                        resultCell = cells[3].GetAttribute("textContent");
                    }
                }

                if (resultCell != null)
                {
                    Logger.LogInformation($"Found {resultCell} for {account}");
                    var moneyStateModel = Money(account, double.Parse(resultCell, new NumberFormatInfo() {NumberDecimalSeparator = "."}),
                        CurrencyExtensions.USD);
                    return moneyStateModel;
                }
            }

            throw new NotFoundException();
        }
    }
}