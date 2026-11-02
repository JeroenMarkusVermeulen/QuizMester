// Model: een categorie (bv. Sports/Science). General bestaat niet als aparte databasecategorie.
namespace Quizmester.Models;

public sealed class Category
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public override string ToString() => Name;
}
