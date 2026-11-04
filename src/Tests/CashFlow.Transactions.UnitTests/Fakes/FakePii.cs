using CashFlow.Transactions.Application.Abstractions.Messaging;
using CashFlow.Transactions.Application.Interfaces;
using CashFlow.Transactions.Domain.Exceptions;
using CashFlow.Transactions.Domain.Interfaces.Common;

namespace CashFlow.Transactions.UnitTests.Fakes;

internal sealed class FakePii : IPiiProtector
{
    public string Encrypt(string plainText) => "enc:" + plainText;

    public string Decrypt(string cipherText) => cipherText["enc:".Length..];

    public string BlindIndex(string value) => "h:" + value;
}
