using Newtonsoft.Json;

namespace Helper
{
    public class ExternalRequest
    {
        [JsonProperty("type")]
        public string Type { get; set; }

        [JsonProperty("processResponse", NullValueHandling = NullValueHandling.Ignore)]
        public ProcessResponse ProcessResponse { get; set; }
    }

    public class ProcessResponse
    {
        [JsonProperty("conviva", NullValueHandling = NullValueHandling.Ignore)]
        public ConvivaResponse Conviva { get; set; }

        [JsonProperty("peacock", NullValueHandling = NullValueHandling.Ignore)]
        public PeacockResponse Peacock { get; set; }

        [JsonProperty("eventName")]
        public string EventName { get; set; }
    }

    public class ConvivaResponse
    {
        public string Status { get; set; }
    }

    public class PeacockResponse
    {
        public string Status { get; set; }
    }
}