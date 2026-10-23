using IAApi.IntelligenceLab.Observability;

namespace IAApi.Tests.IntelligenceLab;

public sealed class ReadingAcquisitionDiagnosticTests
{
    private static DiagnosticContext Context() => DiagnosticContext.Create(
        "correlation-acquisition",
        "clp-run-01",
        new DateTimeOffset(2026, 10, 23, 14, 0, 0, TimeSpan.FromHours(-3)));

    [Fact]
    public void ValidReading_ZeroIsPreservedAndSourceTimestampIsUtc()
    {
        var diagnostic = new ReadingAcquisitionDiagnostic(
            Context(), "cliente-simulado", "BOMBA.VAZAO",
            ReadingAcquisitionStatus.Valid, validValue: 0,
            sourceTimestampUtc: new DateTimeOffset(2026, 10, 23, 13, 59, 0, TimeSpan.FromHours(-3)));

        Assert.Equal(ReadingAcquisitionStatus.Valid, diagnostic.Status);
        Assert.Equal(0d, diagnostic.ValidValue);
        Assert.Equal(TimeSpan.Zero, diagnostic.SourceTimestampUtc!.Value.Offset);
        Assert.Null(diagnostic.ReasonCode);
        Assert.Null(diagnostic.SafeReason);
        Assert.Equal("clp-run-01", diagnostic.Context.RunId);
    }

    [Fact]
    public void MissingReading_RequiresReasonsAndCannotPretendToContainAValue()
    {
        Assert.Throws<ArgumentException>(() => new ReadingAcquisitionDiagnostic(
            Context(), "cliente-simulado", "BOMBA.VAZAO",
            ReadingAcquisitionStatus.Missing, validValue: null));

        Assert.Throws<ArgumentException>(() => new ReadingAcquisitionDiagnostic(
            Context(), "cliente-simulado", "BOMBA.VAZAO",
            ReadingAcquisitionStatus.Missing, validValue: 0,
            reasonCode: "SAMPLE_TIMEOUT", safeReason: "Sem amostra no intervalo esperado."));

        Assert.Throws<ArgumentException>(() => new ReadingAcquisitionDiagnostic(
            Context(), "cliente-simulado", "BOMBA.VAZAO",
            ReadingAcquisitionStatus.Missing, validValue: null,
            sourceTimestampUtc: DateTimeOffset.UtcNow,
            reasonCode: "SAMPLE_TIMEOUT", safeReason: "Sem amostra no intervalo esperado."));

        var diagnostic = new ReadingAcquisitionDiagnostic(
            Context(), "cliente-simulado", "BOMBA.VAZAO",
            ReadingAcquisitionStatus.Missing, validValue: null,
            reasonCode: " SAMPLE_TIMEOUT ",
            safeReason: " Sem amostra no intervalo esperado. ");

        Assert.Null(diagnostic.ValidValue);
        Assert.Null(diagnostic.SourceTimestampUtc);
        Assert.Equal("SAMPLE_TIMEOUT", diagnostic.ReasonCode);
        Assert.Equal("Sem amostra no intervalo esperado.", diagnostic.SafeReason);
    }

    [Fact]
    public void InvalidReading_IsRepresentedWithoutNaNOrInfinityPayload()
    {
        foreach (var invalidValue in new[] { double.NaN, double.PositiveInfinity, double.NegativeInfinity })
        {
            Assert.Throws<ArgumentException>(() => new ReadingAcquisitionDiagnostic(
                Context(), "cliente-simulado", "BOMBA.VAZAO",
                ReadingAcquisitionStatus.Invalid, validValue: invalidValue,
                reasonCode: "NON_FINITE", safeReason: "Valor numérico inválido."));
        }

        var diagnostic = new ReadingAcquisitionDiagnostic(
            Context(), "cliente-simulado", "BOMBA.VAZAO",
            ReadingAcquisitionStatus.Invalid, validValue: null,
            sourceTimestampUtc: new DateTimeOffset(2026, 10, 23, 13, 59, 0, TimeSpan.FromHours(-3)),
            reasonCode: " NON_FINITE ", safeReason: " Valor numérico inválido. ");

        Assert.Null(diagnostic.ValidValue);
        Assert.Equal("NON_FINITE", diagnostic.ReasonCode);
        Assert.Equal(TimeSpan.Zero, diagnostic.SourceTimestampUtc!.Value.Offset);
    }

    [Fact]
    public void ValidReading_RequiresFiniteValueAndSourceTimestamp()
    {
        Assert.Throws<ArgumentException>(() => new ReadingAcquisitionDiagnostic(
            Context(), "cliente-simulado", "BOMBA.VAZAO",
            ReadingAcquisitionStatus.Valid, validValue: null,
            sourceTimestampUtc: DateTimeOffset.UtcNow));

        Assert.Throws<ArgumentOutOfRangeException>(() => new ReadingAcquisitionDiagnostic(
            Context(), "cliente-simulado", "BOMBA.VAZAO",
            ReadingAcquisitionStatus.Valid, validValue: double.NaN,
            sourceTimestampUtc: DateTimeOffset.UtcNow));

        Assert.Throws<ArgumentException>(() => new ReadingAcquisitionDiagnostic(
            Context(), "cliente-simulado", "BOMBA.VAZAO",
            ReadingAcquisitionStatus.Valid, validValue: 25));
    }

    [Fact]
    public void AcquisitionStatus_RejectsUnknownAndContradictoryStates()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new ReadingAcquisitionDiagnostic(
            Context(), "cliente-simulado", "BOMBA.VAZAO",
            (ReadingAcquisitionStatus)99, validValue: null));

        Assert.Throws<ArgumentException>(() => new ReadingAcquisitionDiagnostic(
            Context(), "cliente-simulado", "BOMBA.VAZAO",
            ReadingAcquisitionStatus.Valid, validValue: 100,
            sourceTimestampUtc: DateTimeOffset.UtcNow,
            reasonCode: "ERROR", safeReason: "Não deveria existir."));

        Assert.Throws<ArgumentException>(() => new ReadingAcquisitionDiagnostic(
            Context(), "cliente-simulado", "BOMBA.VAZAO",
            ReadingAcquisitionStatus.Invalid, validValue: null,
            reasonCode: " ", safeReason: "Valor rejeitado."));
    }
}
