namespace v1;

message template Request<T>
{
    @used_ids = 4;
    T Data = 0;
    array<byte> CorrelationId = 1;
}

tag template Response<T>
{
    Ok(T) = 0;
    ValidationError = 1;
}

service GetStatus
{
    @id = 1;
    @input = Request<void>;
    @output = Response<void>;
}

materialize Request<int32>, Request<uuid>;