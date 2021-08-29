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

            driver.Navigate().GoToUrl("https://ru.investing.com/indices/us-spx-500");
            var moneyStateModel = ParseMoney("SP500", driver);
            result.Add(moneyStateModel);

            foreach (var item in CurrencyExtensions.KnownCurrencies.Where(v => v != CurrencyExtensions.RUB))
            {
                driver.Navigate().GoToUrl($"https://ru.investing.com/currencies/{item.ToLower()}-rub");
                var itemRub = item + "/" + CurrencyExtensions.RUB;
                var msm = ParseMoney(itemRub, driver);
                result.Add(msm);
            }

            return result;
        }

        private MoneyStateModel ParseMoney(string account, ChromeDriver driver)
        {
            var webElements = GetElements(driver, By.Id("instrument-header-details"));
            var priceElement = webElements.First(v => v.GetAttribute("class").Contains("_last"));
            var sps = priceElement.Text;
            var moneyStateModel = Money(account, double.Parse(sps, new NumberFormatInfo() {NumberDecimalSeparator = ",", NumberGroupSeparator = "."}),
                CurrencyExtensions.USD);
            return moneyStateModel;
        }
    }
}