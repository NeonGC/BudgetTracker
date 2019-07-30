﻿using System;
using System.Collections.Generic;
using System.Linq;
using BudgetTracker.Controllers.ViewModels.Table;
using BudgetTracker.JsModel;
using BudgetTracker.JsModel.Attributes;
using BudgetTracker.Model;

namespace BudgetTracker.Controllers.ViewModels.Widgets
{
    public class ChartWidgetViewModel : WidgetViewModel
    {
        public ChartWidgetViewModel(string providerName, string accountName,
            ChartKind kind,
            TableViewModel vm, bool exemptTransfers) : base(null, null)
        {
            ChartKind = kind;
            ExemptTransfers = exemptTransfers;
            Title = accountName;
            LoadData(vm, providerName, accountName);
        }

        public ChartWidgetViewModel(WidgetModel model, int? period, TableViewModel vm) :
            base(model, new ChartWidgetSettings(model.Properties.ToDictionary(v => v.Key, v => v.Value)))
        {
            var chartWidgetSettings = (ChartWidgetSettings) Settings;

            ChartKind = chartWidgetSettings.ChartKind;
            Title = Title ?? chartWidgetSettings.AccountName;
            Period = period;

            LoadData(vm, chartWidgetSettings.ProviderName, chartWidgetSettings.AccountName);
        }

        public int? Period { get; set; }

        public ChartKind ChartKind { get; set; }
        public bool ExemptTransfers { get; }

        private void LoadData(TableViewModel vm, string providerName, string accountName)
        {
            var column = vm.Headers.First(v =>
                v.Provider == providerName &&
                (v.AccountName == accountName ||
                 v.UserFriendlyName == accountName));

            var columnsToChart = new List<MoneyColumnMetadataJsModel> {column};

            if (column.IsComputed && column.ChartList.Any())
            {
                columnsToChart = vm.Headers.Where(v =>
                    column.ChartList.Contains(v.Provider + "/" + v.AccountName) ||
                    column.ChartList.Contains(v.UserFriendlyName)).ToList();

                if (ChartKind == ChartKind.Linear)
                {
                    columnsToChart.Add(column);
                }
            }

            var chartItems = new List<ChartItem>();

            foreach (var row in vm.Values.Where(v => IsApplicable(v.When, Period)))
            {
                foreach (var header in columnsToChart)
                {
                    var item = row.CalculatedCells.GetValueOrDefault(header);

                    var value = ExemptTransfers ? item?.AdjustedValue : item?.Value;
                    if (value == null)
                        continue;

                    chartItems.Add(new ChartItem
                    {
                        When = row.When,
                        Name = (header.IsComputed ? "" : (header.Provider + "/")) +
                               (header.UserFriendlyName ?? header.AccountName),
                        Value = value.Value,
                        Ccy = item.Ccy
                    });
                }
            }

            var names = chartItems.Select(v => v.Name).Distinct().ToList();
            Dates = chartItems.Select(v => v.When).Distinct().ToList();

            var ci = chartItems.GroupBy(v => v.Name).ToDictionary(v => v.Key, v => v.ToDictionary(s => s.When, s => s));

            Values = names
                .SelectMany(name => Dates.Select(date => (name, date)))
                .Select(s => (s.name, s.date, ci.GetValueOrDefault(s.name)?.GetValueOrDefault(s.date)))
                .GroupBy(v => v.name)
                .ToDictionary(v => v.Key, v => v.Select(s => s.Item3).Where(s => s != null).ToList())
                .Select(v=>new ChartValue
                {
                    Label = v.Key,
                    Values = v.Value
                }).ToList();
        }

        public List<DateTime> Dates { get; set; }
        public List<ChartValue> Values { get; set; }

        public override int Columns => 4;
        public override int Rows => 2;
    }

    [ExportJsModel]
    public class ChartValue
    {
        public string Label { get; set; }

        public List<ChartItem> Values { get; set; }
    }

    [ExportJsModel]
    public class ChartItem
    {
        public DateTime When { get; set; }
        public string Name { get; set; }
        public double Value { get; set; }
        public string Ccy { get; set; }
    }
}