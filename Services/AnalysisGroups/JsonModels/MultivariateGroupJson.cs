public class MultivariateGroupJson
{
    public string Name { get; set; } = "";

    public bool Enabled { get; set; }

    public int MinimumAvailableTags { get; set; }

    public int MaxLagMinutes { get; set; }

    public List<string> Tags { get; set; } = [];
}