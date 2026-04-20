namespace DictoriumDemo.Models;

public enum StructureCategory
{
    HashTable,
    Trees,
    AbsoluteO1,
    ProbabilisticSearch
}

public class DataStructureCategory
{
    public StructureCategory Id { get; set; }
    public string Name { get; set; } = "";
    public string NameRu { get; set; } = "";
    public string Description { get; set; } = "";
    public string Icon { get; set; } = "";
    public string AccentColor { get; set; } = "";
    public string Tag { get; set; } = "";
    public List<DataStructureInfo> Structures { get; set; } = new();
}

public class DataStructureInfo
{
    public int IssueNumber { get; set; }
    public string Name { get; set; } = "";
    public string NameRu { get; set; } = "";
    public string Description { get; set; } = "";
    public string Slug { get; set; } = "";
    public StructureCategory Category { get; set; }

    // Complexity info
    public string TimeComplexitySearch { get; set; } = "";
    public string TimeComplexityInsert { get; set; } = "";
    public string TimeComplexityDelete { get; set; } = "";
    public string SpaceComplexity { get; set; } = "";

    // Key characteristics
    public List<string> Pros { get; set; } = new();
    public List<string> Cons { get; set; } = new();
    public List<string> UseCases { get; set; } = new();
}
