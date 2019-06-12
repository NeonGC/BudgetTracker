﻿using System.Collections.Generic;
using BudgetTracker.JsModel;
using BudgetTracker.Model;

namespace BudgetTracker.Controllers.ViewModels.Table
{
    public class ReferenceExpression : CalculateExpression
    {
        private readonly MoneyColumnMetadataJsModel _column;

        private bool _evaluated;
        
        public ReferenceExpression(MoneyColumnMetadataJsModel column)
        {
            _column = column;
        }

        public override void Evaluate(Dictionary<MoneyColumnMetadataJsModel, CalculatedResult> dependencies)
        {
            if (!_evaluated)
            {
                _evaluated = true;

                dependencies.TryGetValue(_column, out var matchedDependency);

                Value = matchedDependency?.Value == null
                        ? CalculatedResult.ResolutionFail(_column, _column.Provider + "/" + _column.AccountName)
                        : matchedDependency;
            }
        }
        
        public override CalculateExpression TryApply(CalculateExpression otherExpression) => throw new System.NotImplementedException();

        protected override string ToStringImpl() => $"[{_column.Provider}/{_column.UserFriendlyName ?? _column.AccountName}]({Value})";
    }
}