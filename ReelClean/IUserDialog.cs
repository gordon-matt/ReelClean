namespace ReelClean;

public interface IUserDialog
{
    Task<bool> ConfirmAsync(string title, string message);
}
