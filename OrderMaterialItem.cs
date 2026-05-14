using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace ComputerServiceManager.Models
{
    public class OrderMaterialItem : INotifyPropertyChanged
    {
        private int _idПозиции;
        private int _idМатериала;
        private string _наименование;
        private decimal _количество;
        private decimal _ценаЗаЕдиницу;
        private decimal _стоимостьПозиции;
        private int? _idСтатус; // Новый статус: 8=Резерв, 9=Списан
        private bool _isNew;
        private int? _idТипМатериала;

        // ID позиции (если есть в БД)
        public int idПозиции { get => _idПозиции; set { _idПозиции = value; OnPropertyChanged(); } }

        // ID материала
        public int idМатериала { get => _idМатериала; set { _idМатериала = value; OnPropertyChanged(); } }

        public string Наименование { get => _наименование; set { _наименование = value; OnPropertyChanged(); CalculateTotal(); } }

        public decimal Количество
        {
            get => _количество;
            set { _количество = value; OnPropertyChanged(); CalculateTotal(); }
        }

        public decimal ЦенаЗаЕдиницу
        {
            get => _ценаЗаЕдиницу;
            set { _ценаЗаЕдиницу = value; OnPropertyChanged(); CalculateTotal(); }
        }

        public decimal СтоимостьПозиции
        {
            get => _стоимостьПозиции;
            set { _стоимостьПозиции = value; OnPropertyChanged(); }
        }

        /// <summary>
        /// ID статуса материала (8=Резерв, 9=Списан)
        /// </summary>
        public int? idСтатус 
        { 
            get => _idСтатус; 
            set { _idСтатус = value; OnPropertyChanged(); OnPropertyChanged(nameof(СтатусТекст)); } 
        }

        public string СтатусТекст
        {
            get
            {
                switch (_idСтатус)
                {
                    case 8: return "Резерв";
                    case 9: return "Списан";
                    default: return "Не указан";
                }
            }
        }

        // Флаг новой записи (еще не сохраненной в БД)
        public bool IsNew { get => _isNew; set { _isNew = value; OnPropertyChanged(); } }

        // Ссылка на сущность БД (опционально, если нужно для отладки)
        public СоставЗаказа_Материалы DbEntity { get; set; }

        private void CalculateTotal()
        {
            СтоимостьПозиции = Количество * ЦенаЗаЕдиницу;
        }

        public event PropertyChangedEventHandler PropertyChanged;
        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }

    public class OrderServiceItem : INotifyPropertyChanged
    {
        private int _idПозиции;
        private int _idУслуги;
        private string _наименование;
        private int _количество;
        private decimal _ценаЗаЕдиницу;
        private decimal _стоимостьПозиции;
        private bool _статусПозиции;
        private bool _isNew;
        private string _примечание;

        public int idПозиции { get => _idПозиции; set { _idПозиции = value; OnPropertyChanged(); } }
        public int idУслуги { get => _idУслуги; set { _idУслуги = value; OnPropertyChanged(); } }
        public string Наименование { get => _наименование; set { _наименование = value; OnPropertyChanged(); CalculateTotal(); } }

        public int Количество
        {
            get => _количество;
            set { _количество = value; OnPropertyChanged(); CalculateTotal(); }
        }

        public decimal ЦенаЗаЕдиницу
        {
            get => _ценаЗаЕдиницу;
            set { _ценаЗаЕдиницу = value; OnPropertyChanged(); CalculateTotal(); }
        }

        public decimal СтоимостьПозиции
        {
            get => _стоимостьПозиции;
            set { _стоимостьПозиции = value; OnPropertyChanged(); }
        }

        public bool СтатусПозиции { get => _статусПозиции; set { _статусПозиции = value; OnPropertyChanged(); } }
        public bool IsNew { get => _isNew; set { _isNew = value; OnPropertyChanged(); } }
        public string Примечание { get => _примечание; set { _примечание = value; OnPropertyChanged(); } }

        public СоставЗаказа_Услуги DbEntity { get; set; }

        private void CalculateTotal()
        {
            СтоимостьПозиции = Количество * ЦенаЗаЕдиницу;
        }

        public event PropertyChangedEventHandler PropertyChanged;
        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}