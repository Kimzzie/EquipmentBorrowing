using System.Collections.Generic;

namespace EquipmentBorrowing.Application.Dtos;

/// <summary>Who or what the history is about, plus its records (newest first).</summary>
public sealed record BorrowingHistory(
    string Title,
    string Subtitle,
    IReadOnlyList<BorrowingRecord> Records);