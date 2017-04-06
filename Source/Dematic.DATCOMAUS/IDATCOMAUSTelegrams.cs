namespace Dematic.DATCOMAUS
{
    public interface IDATCOMAUSTelegrams
    {
        string ReceiverIdentifier { get; }
        string SenderIdentifier { get; }
        TelegramTemplate Template { get; }
        int GetTelegramLength(TelegramTypes telegramType);
    }
}