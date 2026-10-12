using System;

namespace tpkursa4
{
    public class DataInboundGateway
    {
        private readonly IWeatherRepository _repository;
        private readonly Validator _validator;

        public DataInboundGateway(IWeatherRepository repository)
        {
            _repository = repository ?? throw new ArgumentNullException(nameof(repository));
            _validator = new Validator(_repository); // Используем наш валидатор
        }

        public void ProcessIncomingData(Settlement settlement, WeatherMeasurement data)
        {
            // Используем комплексную проверку, включая температуру, влажность и давление
            if (_validator.IsValid(data, settlement))
            {
                _repository.SaveMeasurement(settlement, data);
            }
        }
    }
}