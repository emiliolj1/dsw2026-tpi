using System.Text.Json.Serialization;

namespace Dsw2026Tpi.CrossCutting.Models;

public record ErrorResponse(string ErrorCode, string Message)
{
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public ICollection<ErrorDetail>? Details { get; private set; }

    public void AddDetail(string field, string issue)
    {
        Details ??= [];
        Details.Add(new ErrorDetail(field, issue));
    }

    public void AddDetail(IEnumerable<(string, string)> details)
    {
        foreach (var detail in details)
        {
            AddDetail(detail.Item1, detail.Item2);
        }
    }
}

public record ErrorDetail(string Field, string Issue);