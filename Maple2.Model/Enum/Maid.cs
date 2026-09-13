namespace Maple2.Model.Enum;

public enum MaidMood : byte {
    Normal = 0, // (So-so)
    Good = 1,
    VeryGood = 2,
}

/// <summary>maidsalary.xml SalaryType: which currency the salary is charged in.</summary>
public enum MaidSalaryType : byte {
    Meso = 0,
    Meret = 1,
}
