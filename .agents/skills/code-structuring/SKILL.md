---
name: code-structuring
description: Organize or refactor source code when a task requires decisions about modules, directories, file responsibilities, boundaries, or dependency direction. Do not use for edits that preserve the existing structure.
---

# Code structuring

Use feature slices to organize code. A feature slice is a directory that contains all the code related to a single feature on its respective module (UI, backend, etc.).

Avoid primitive obsession. Refactor when a primitive type is used to represent a concept that has its own behavior or rules.

Only create interfaces when truly necessary. Avoid creating interfaces for the sake of it.

Composition over inheritance.

Prefer static classes if it would lead to a pure function approach.

Use C# union types for discriminated unions. Example:

```csharp
public record class Meters(double Value);
public record class Feet(double Value);

public union Length(Meters, Feet)
{
    public double TotalMeters => this switch
    {
        Meters m => m.Value,
        Feet f => f.Value * 0.3048,
        _ => throw new InvalidOperationException("The Length has no value."),
    };

    public Length Add(Length other) => new Meters(TotalMeters + other.TotalMeters);
}
```
