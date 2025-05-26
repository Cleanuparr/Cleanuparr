﻿using Newtonsoft.Json;

namespace Data.Models.Deluge.Response;

public sealed record DelugeResponse<T>
{
    [JsonProperty(PropertyName = "id")]
    public int ResponseId { get; set; }

    [JsonProperty(PropertyName = "result")]
    public T? Result { get; set; }

    [JsonProperty(PropertyName = "error")]
    public DelugeError? Error { get; set; }
}