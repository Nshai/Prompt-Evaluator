namespace AiPromptEvaluator;

/// <summary>
/// The names the code uses to read the stored canonical model.
///
/// These are the shape contract between extraction and assessment, and they have been the most
/// expensive strings in the system to get wrong. A charge the assessor needed to divide was
/// chased across three stages and missed twice, because the extraction wrote it under
/// <c>existingArrangements[].charges.lines[]</c>, then under <c>costsAndCharges.*.lines[]</c>,
/// then as flat <c>…Percentage</c> / <c>…Amount</c> siblings on a replacement-analysis entry —
/// and each attempt named its path inline, three files apart, where nothing showed that the set
/// had grown.
///
/// Collected here, the shapes the pipeline knows how to read are a list somebody can look at.
///
/// Only names the <em>code</em> reaches for are here. Names bound by
/// <c>[JsonPropertyName]</c> on a record stay on the record: the attribute is already the
/// declaration, and a constant beside it would be a second place to change.
/// </summary>
public static class CanonicalModel
{
    /// <summary>Sections addressed by JSON Pointer.</summary>
    public static class Paths
    {
        /// <summary>What the extraction reported about its own run — gaps, ambiguities, contradictions.</summary>
        public const string ExtractionReport = "/extractionReport";
    }

    /// <summary>Top-level sections read directly by the pipeline.</summary>
    public const string ExistingArrangements = "existingArrangements";
    public const string CostsAndCharges = "costsAndCharges";
    public const string FinancialPosition = "financialPosition";

    /// <summary>What identifies an arrangement, in the order the code prefers to name it by.</summary>
    public const string Provider = "provider";
    public const string ProductName = "productName";
    public const string ArrangementId = "arrangementId";
    public const string Scope = "scope";

    /// <summary>What an arrangement is worth, and what is being moved out of it.</summary>
    public const string CurrentValue = "currentValue";
    public const string TransferValue = "transferValue";

    /// <summary>Charges, in each of the shapes the extraction has used for them.</summary>
    public const string Charges = "charges";
    public const string Lines = "lines";
    public const string TotalOngoingPercentage = "totalOngoingPercentage";
    public const string Percentage = "percentage";
    public const string Amount = "amount";
    public const string Value = "value";
    public const string ChargeType = "chargeType";
    public const string Description = "description";

    /// <summary>
    /// The suffixes that make a pair. Any object carrying <c>&lt;prefix&gt;Percentage</c> and
    /// <c>&lt;prefix&gt;Amount</c> is describing one charge, whatever the schema around it is
    /// called — which is how the third shape is found without naming a fourth path.
    /// </summary>
    public const string PercentageSuffix = "Percentage";
    public const string AmountSuffix = "Amount";

    /// <summary>Income, and the parts of it the frequency arithmetic needs.</summary>
    public const string Income = "income";
    public const string Net = "net";
    public const string Gross = "gross";
    public const string Frequency = "frequency";
    public const string Category = "category";

    /// <summary>
    /// What a switch is stated to cost, and what it is stated to achieve.
    ///
    /// These two are the same quantity written down twice, and a report that disagrees with
    /// itself across them is stating that a recommendation costs more every year and changes the
    /// outcome by nothing. Read together by <see cref="DerivedFigures"/>, because four runs on two
    /// models were given both halves and never put them side by side.
    /// </summary>
    public const string Comparison = "comparison";
    public const string DifferencePercentage = "differencePercentage";
    public const string Direction = "direction";
    public const string ReductionInYield = "reductionInYield";
    public const string Subject = "subject";
    public const string GrowthRateBasis = "growthRateBasis";
    public const string RiyPercentage = "riyPercentage";
    public const string EffectOfSwitchingAmount = "effectOfSwitchingAmount";
    public const string EffectOfSwitchingPercentage = "effectOfSwitchingPercentage";

    /// <summary>
    /// A recorded performance figure. Cumulative over a period, which is what makes an
    /// annualisation possible and a simple mean wrong.
    /// </summary>
    public const string CumulativeReturnPercentage = "cumulativeReturnPercentage";
    public const string Period = "period";
    public const string InstrumentName = "instrumentName";
    public const string Role = "role";
}
