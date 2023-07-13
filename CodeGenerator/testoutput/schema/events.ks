namespace v1;

message template UserData<T>
{
    uuid UserId = 1;
    T Data = 2;
}

message UserAttributes
{
    string UserName = 2;
    string Email = 3;
}

tag Events
{
    UNKNOWN = 0;
    UserCreated(UserData<UserAttributes>) = 1;
    UserDeleted(UserData<void>) = 2;
    UserModified(UserData<UserAttributes>) = 3;
}
