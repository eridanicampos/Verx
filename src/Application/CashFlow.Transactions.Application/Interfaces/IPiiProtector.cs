namespace CashFlow.Transactions.Application.Interfaces;

public interface IPiiProtector
{
    string Encrypt(string plainText);

    string Decrypt(string cipherText);

    string BlindIndex(string value);
}
