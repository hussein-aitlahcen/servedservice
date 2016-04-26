# ServedService
Easily serve your service over network

## Shared library
```C#
public interface ICalculator
{
    int Compute(Operation operation);
}

public enum OperationType
{
    Add,
    Mul,
    Sub,
    Xor,
    Or,
    And
}

[ProtoContract(ImplicitFields = ImplicitFields.AllPublic)]
public sealed class Operation
{
    public OperationType Type { get; set; }
    public int A { get; set; }
    public int B { get; set; }
}
```

## Server application
```C#
public sealed class CalculatorImpl : ICalculator
{
    public int Compute(Operation operation)
    {
        switch (operation.Type)
        {
            case OperationType.Add:
                return operation.A + operation.B;
            case OperationType.Sub:
                return operation.A - operation.B;
            case OperationType.Mul:
                return operation.A*operation.B;
            case OperationType.And:
                return operation.A & operation.B;
            case OperationType.Or:
                return operation.A | operation.B;
            case OperationType.Xor:
                return operation.A ^ operation.B;
        }
        throw new InvalidOperationException("Unknow operation type " + operation.Type);
    }
}

new Servant("127.0.0.1", 4444)
                .Serve<ICalculator>("com.servedservice.calculator", new CalculatorImpl())
                .Start();
Console.ReadLine();
```

## Client application
```C#
 var proxy = new ServiceProxy("127.0.0.1", 4444)
                .GetService<ICalculator>("com.servedservice.calculator");
  var result = proxy.Compute(new Operation()
  {
      Type = OperationType.Or,
      A = 8,
      B = 1,
  });
```
