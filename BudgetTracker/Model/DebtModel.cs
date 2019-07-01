using System;
using System.Collections.Generic;
using OutCode.EscapeTeams.ObjectRepository;
using OutCode.EscapeTeams.ObjectRepository.AzureTableStorage;

namespace BudgetTracker.Model
{
    public sealed class DebtModel : ModelBase
    {
        public class DebtEntity : BaseEntity
        {
            public double Amount { get; set; }

            public string Ccy { get; set; }

            public DateTime When { get; set; }

            public int DaysCount { get; set; }

            public double Percentage { get; set; }

            public string Description { get; set; }
            public string RegexForTransfer { get; set; }
        }

        private readonly DebtEntity _entity;

        public DebtModel(DebtEntity entity)
        {
            _entity = entity;
        }

        public DebtModel()
        {
            _entity = new DebtEntity
            {
                Id = Guid.NewGuid()
            };
        }

        protected override BaseEntity Entity => _entity;

        public string Description
        {
            get => _entity.Description;
            set => UpdateProperty(() => () => _entity.Description, value);
        }

        public IEnumerable<PaymentModel> Payments => Multiple<PaymentModel>(() => x => x.DebtId);
        
        public double Amount
        {
            get => _entity.Amount;
            set => UpdateProperty(() => () => _entity.Amount, value);
        }

        public DateTime When
        {
            get => _entity.When;
            set => UpdateProperty(() => () => _entity.When, value);
        }

        public int DaysCount
        {
            get => _entity.DaysCount;
            set => UpdateProperty(() => () => _entity.DaysCount, value);
        }

        public double Percentage
        {
            get => _entity.Percentage;
            set => UpdateProperty(() => () => _entity.Percentage, value);
        }

        public string Ccy
        {
            get => _entity.Ccy;
            set => UpdateProperty(() => () => _entity.Ccy, value);
        }

        public string RegexForTransfer
        {
            get => _entity.RegexForTransfer;
            set => UpdateProperty(() => () => _entity.RegexForTransfer, value);
        }
    }
}