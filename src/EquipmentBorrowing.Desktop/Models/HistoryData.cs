// Models/HistoryData.cs
using EquipmentBorrowing.Desktop.Api;

namespace EquipmentBorrowing.Desktop.Models;

public sealed record HistoryData(string Title, string Subtitle, IReadOnlyList<BorrowingDto> Records);
