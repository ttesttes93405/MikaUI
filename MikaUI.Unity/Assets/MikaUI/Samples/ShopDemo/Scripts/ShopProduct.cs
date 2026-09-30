public sealed class ShopProduct
{
    public string Name { get; }
    public int Price { get; }
    public string Description { get; }

    public ShopProduct(string name, int price, string description)
    {
        Name = name;
        Price = price;
        Description = description;
    }
}
