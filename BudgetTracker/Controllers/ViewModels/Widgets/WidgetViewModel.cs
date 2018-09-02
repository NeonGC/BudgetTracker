﻿using System;
using BudgetTracker.Model;

namespace BudgetTracker.Controllers.ViewModels.Widgets
{
    public abstract class WidgetViewModel
    {
        protected const string MiddleDash = "—";
        
        private readonly WidgetModel _model;

        public WidgetViewModel(WidgetModel model, WidgetSettings settings)
        {
            Settings = settings;
            _model = model;
            Title = _model?.Title;
            Id = _model?.Id;
        }
        
        protected bool IsApplicable(DateTime argWhen, int? period)
        {
            if (period == null || period == 0)
                return true;

            return argWhen.AddMonths(period.Value) > DateTime.Now;
        }

        protected WidgetSettings Settings { get; }

        public abstract string TemplateName { get; }

        public string Title { get; protected set; }
        public Guid? Id {get; protected set; }
        public abstract int Columns {get;}
        public virtual int Rows => 1;
    }
}