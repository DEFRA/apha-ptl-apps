using PTL.Contracts.Distribution;
using PTL.Core.Distribution;

namespace PTL.Api.Tests.Distribution;

// Legacy Scheduling/Tabulation.aspx.vb + PublishedTabulation.aspx.vb BuildTables, the Write*Columns
// print data and the ResultsTabulation ratings arithmetic.
public class TabulationReportBuilderTests
{
    private static readonly Guid TestId = Guid.Parse("10000000-0000-0000-0000-000000000001");
    private static readonly Guid SampleA = Guid.Parse("20000000-0000-0000-0000-00000000000a");
    private static readonly Guid SampleB = Guid.Parse("20000000-0000-0000-0000-00000000000b");
    private static readonly Guid SampleTestA = Guid.Parse("21000000-0000-0000-0000-00000000000a");
    private static readonly Guid Method1 = Guid.Parse("30000000-0000-0000-0000-000000000001");
    private static readonly Guid Result1 = Guid.Parse("40000000-0000-0000-0000-000000000001");
    private static readonly Guid Category1 = Guid.Parse("50000000-0000-0000-0000-000000000001");
    private static readonly Guid Category2 = Guid.Parse("50000000-0000-0000-0000-000000000002");
    private static readonly Guid P1 = Guid.Parse("60000000-0000-0000-0000-000000000001");
    private static readonly Guid P2 = Guid.Parse("60000000-0000-0000-0000-000000000002");
    private static readonly Guid P3 = Guid.Parse("60000000-0000-0000-0000-000000000003");
    private static readonly Guid P4 = Guid.Parse("60000000-0000-0000-0000-000000000004");
    private static readonly Guid Viewer1 = Guid.Parse("70000000-0000-0000-0000-000000000001");
    private static readonly DateTime Submitted = new(2026, 5, 10);

    private static TabulationSource Source(
        bool published = false,
        bool assess = false,
        bool hasRatings = false,
        bool intendedOnly = false,
        bool single = false,
        bool viewers = false,
        string author = "",
        string dept = "") =>
        new()
        {
            TabulationId = Guid.Parse("80000000-0000-0000-0000-000000000001"),
            DistributionTabulationId = Guid.Parse("80000000-0000-0000-0000-000000000002"),
            IsPublished = published,
            Distribution = "D1a/S1",
            SchemeIdentifier = "BT",
            SchemeName = "Brucella",
            TabulationName = "Summary",
            RequiresAssessment = assess,
            DistributionDate = new DateTime(2026, 5, 3),
            SchemeVersionDate = new DateTime(2025, 4, 1),
            HasRatings = hasRatings,
            IntendedResultsOnly = intendedOnly,
            SingleParticipantTabulation = single,
            AvailableToViewers = viewers,
            TestConsultantCommentsAuthor = author,
            TestConsultantCommentsDept = dept,
            Signatory = "Dr Signatory",
            CreationDate = new DateTime(2026, 6, 10, 14, 30, 0),
        };

    // One test, two samples, one method item and one result item (no categories).
    private static TabulationSourceTest PlainTest()
    {
        var test = new TabulationSourceTest { TestId = TestId, Name = "Culture", IsAssessmentSubmitted = true };
        test.Samples.Add(new(SampleA, SampleTestA, "S1", "Positive"));
        test.Samples.Add(new(SampleB, null, "S2", "Negative"));
        test.MethodItems.Add(new(Method1, "Method"));
        test.ResultItems.Add(new(Result1, "Result"));
        return test;
    }

    // One test, one sample, one method, one result and two category items.
    private static TabulationSourceTest AssessedTest(bool submitted = true)
    {
        var test = new TabulationSourceTest { TestId = TestId, Name = "Clinical", IsAssessmentSubmitted = submitted };
        test.Samples.Add(new(SampleA, SampleTestA, "S1", "Brucella abortus"));
        test.MethodItems.Add(new(Method1, "Method"));
        test.ResultItems.Add(new(Result1, "Identity"));
        test.CategoryItems.Add(new(Category1, "Clinical"));
        test.CategoryItems.Add(new(Category2, "Pathology"));
        return test;
    }

    private static TabulationSourceParticipant Participant(Guid id, string labCode, string nilReason = "")
    {
        var participant = new TabulationSourceParticipant { ParticipantId = id, LabCode = labCode, NilReturnReason = nilReason };
        return participant;
    }

    private static TabulationSourceParticipant WithResult(TabulationSourceParticipant participant, Guid sampleId, string result)
    {
        participant.ResultItemResults[(sampleId, Result1)] = result;
        return participant;
    }

    private static TabulationSourceAssessment Assessment(
        int score, DateTime? submission = null, DateTime? nonSubmission = null, DateTime? nonCapable = null, DateTime? assessmentSubmitted = null, string comment = "") =>
        new(score, comment, submission, nonSubmission, nonCapable, assessmentSubmitted);

    private static TabulationReportResponse Build(TabulationSource source, Guid? participantId = null, Guid? viewerId = null) =>
        TabulationReportBuilder.Build(source, participantId ?? Guid.Empty, viewerId ?? Guid.Empty);

    private static (string Text, int ColSpan, int RowSpan, string Kind, bool Nil) C(TabulationCellResponse cell) =>
        (cell.Text, cell.ColSpan, cell.RowSpan, cell.Kind, cell.IsNilReturn);

    private static List<string> Texts(TabulationGridRowResponse row) => [.. row.Cells.Select(c => c.Text)];

    [Fact]
    public void Build_NullSource_Throws() =>
        Assert.Throws<ArgumentNullException>(() => TabulationReportBuilder.Build(null!, Guid.Empty, Guid.Empty));

    [Fact]
    public void Build_Draft_SetsTitleDateTextIdsAndNoSignatory()
    {
        var source = Source();
        source.Attachments.Add(new(Guid.Parse("90000000-0000-0000-0000-000000000001"), "notes.pdf", "application/pdf", [1]));
        source.Attachments.Add(new(Guid.NewGuid(), "second.pdf", "application/pdf", [2]));

        var report = Build(source);

        Assert.Equal("FINAL REPORT for D1a/S1 BT: Brucella", report.Title);
        Assert.Equal("Summary", report.TabulationName);
        Assert.Equal("Date Of Distribution: 03/05/2026", report.DistributionDateText);
        Assert.False(report.IsPublished);
        Assert.Equal(source.TabulationId, report.TabulationId);
        Assert.Equal(source.DistributionTabulationId, report.DistributionTabulationId);
        Assert.Null(report.Signatory);
        Assert.Equal(new TabulationAttachmentResponse(Guid.Parse("90000000-0000-0000-0000-000000000001"), "notes.pdf"), report.Attachment);
        Assert.Empty(report.Tables);
        Assert.Null(report.Ratings);
    }

    [Fact]
    public void Build_Published_UsesDistributionDateLabelAndSignatoryBlockWithFixedAphaAddress()
    {
        var report = Build(Source(published: true));

        Assert.True(report.IsPublished);
        Assert.Equal("Distribution Date: 03/05/2026", report.DistributionDateText);
        var signatory = Assert.IsType<TabulationSignatoryResponse>(report.Signatory);
        Assert.Equal("Version: 01/04/2025", signatory.VersionText);
        Assert.Equal("Dr Signatory", signatory.Signatory);
        Assert.Equal("Issued: 10/06/2026", signatory.IssuedText);
        Assert.Equal(
            ["Animal and Plant Health Agency", "Quality Assurance Unit", "The Elms, College Road", "Sutton Bonington", "Loughborough", "Leicestershire", "LE12 5RB", "UK"],
            signatory.AddressLines);
    }

    [Fact]
    public void Build_PublishedWithoutCreationDate_IssuedTextUsesTheDefaultDate()
    {
        var source = new TabulationSource { IsPublished = true };

        var report = Build(source);

        Assert.Equal("Issued: 01/01/0001", report.Signatory!.IssuedText);
    }

    [Fact]
    public void Build_NoAttachments_AttachmentIsNull() => Assert.Null(Build(Source()).Attachment);

    [Theory]
    [InlineData("Dr A", "QAU", "Comments provided by Dr A, QAU:")]
    [InlineData("Dr A", "", "Comments provided by Dr A:")]
    [InlineData("", "QAU", "Comments , QAU:")]
    [InlineData("", "", "Comments :")]
    public void Build_CommentsTitle_FollowsLegacyConcatenation(string author, string dept, string expected) =>
        Assert.Equal(expected, Build(Source(author: author, dept: dept)).TestConsultantCommentsTitle);

    [Fact]
    public void Build_CommentsHtml_IsSanitisedAndShowUkasLogoPassedThrough()
    {
        var source = new TabulationSource
        {
            Comments = "<p>Sample <b>comment</b></p><script>alert(1)</script>",
            TestConsultantComments = "<p onclick=\"x()\">Consultant</p><iframe src=\"x\"></iframe>",
            StandardTabulationText = "<p>Standard</p><img src=x onerror=alert(1)>",
            ShowUkasLogo = true,
        };

        var report = Build(source);

        Assert.Equal("<p>Sample <b>comment</b></p>alert(1)", report.CommentsHtml);
        Assert.Equal("<p>Consultant</p>", report.TestConsultantCommentsHtml);
        Assert.Equal("<p>Standard</p>", report.StandardTabulationTextHtml);
        Assert.True(report.ShowUkasLogo);
    }

    [Fact]
    public void Build_SingleParticipantTabulation_OffersAllParticipantsFirstAndNoViewers()
    {
        var source = Source(single: true, viewers: true);
        source.Participants.Add(Participant(P1, "L001"));
        source.Participants.Add(Participant(P2, "L002"));
        source.Viewers.Add(new TabulationSourceViewer { ViewerId = Viewer1, Name = "Viewer One" });

        var report = Build(source);

        Assert.Equal(
            [new(Guid.Empty, "All Participants"), new(P1, "L001"), new(P2, "L002")],
            report.ParticipantOptions);
        Assert.Empty(report.ViewerOptions);
        Assert.Equal(Guid.Empty, report.SelectedParticipantId);
        Assert.Equal(Guid.Empty, report.SelectedViewerId);
    }

    [Fact]
    public void Build_SingleParticipantSelected_ShowsOnlyThatParticipantsRows()
    {
        var source = Source(single: true);
        source.Tests.Add(PlainTest());
        source.Participants.Add(WithResult(Participant(P1, "L001"), SampleA, "Pos"));
        source.Participants.Add(WithResult(Participant(P2, "L002"), SampleA, "Neg"));

        var report = Build(source, participantId: P2);

        Assert.Equal(P2, report.SelectedParticipantId);
        var table = Assert.Single(report.Tables);
        Assert.Equal(5, table.Rows.Count);
        Assert.Equal("L002", table.Rows[4].Cells[0].Text);
        Assert.Equal(["L002"], report.Print.Participants.Select(p => p.LabCode));
    }

    [Fact]
    public void Build_UnknownParticipantId_FallsBackToAllParticipants()
    {
        var source = Source(single: true);
        source.Tests.Add(PlainTest());
        source.Participants.Add(WithResult(Participant(P1, "L001"), SampleA, "Pos"));
        source.Participants.Add(WithResult(Participant(P2, "L002"), SampleA, "Neg"));

        var report = Build(source, participantId: Guid.NewGuid());

        Assert.Equal(Guid.Empty, report.SelectedParticipantId);
        Assert.Equal(6, report.Tables[0].Rows.Count);
    }

    [Fact]
    public void Build_AvailableToViewers_OffersNormalViewFirstAndNoParticipantFilter()
    {
        var source = Source(viewers: true);
        source.Participants.Add(Participant(P1, "L001"));
        source.Viewers.Add(new TabulationSourceViewer { ViewerId = Viewer1, Name = "Viewer One" });

        var report = Build(source);

        Assert.Empty(report.ParticipantOptions);
        Assert.Equal([new(Guid.Empty, "Normal View"), new(Viewer1, "Viewer One")], report.ViewerOptions);
        Assert.Equal(Guid.Empty, report.SelectedViewerId);
    }

    [Fact]
    public void Build_AvailableToViewersWithoutViewers_OffersNoFilters()
    {
        var report = Build(Source(viewers: true));

        Assert.Empty(report.ParticipantOptions);
        Assert.Empty(report.ViewerOptions);
    }

    [Fact]
    public void Build_ViewersNotAvailable_IgnoresViewerIdAndOffersNoViewers()
    {
        var source = Source();
        source.Tests.Add(PlainTest());
        source.Participants.Add(WithResult(Participant(P1, "L001"), SampleA, "Pos"));
        source.Participants.Add(WithResult(Participant(P2, "L002"), SampleA, "Neg"));
        var viewer = new TabulationSourceViewer { ViewerId = Viewer1, Name = "Viewer One" };
        viewer.ParticipantIds.Add(P1);
        source.Viewers.Add(viewer);

        var report = Build(source, viewerId: Viewer1);

        Assert.Empty(report.ViewerOptions);
        Assert.Equal(Guid.Empty, report.SelectedViewerId);
        Assert.Equal(6, report.Tables[0].Rows.Count);
    }

    [Fact]
    public void Build_ViewerSelected_RestrictsRowsToTheViewersParticipants()
    {
        var source = Source(viewers: true);
        source.Tests.Add(PlainTest());
        source.Participants.Add(WithResult(Participant(P1, "L001"), SampleA, "Pos"));
        source.Participants.Add(WithResult(Participant(P2, "L002"), SampleA, "Neg"));
        var viewer = new TabulationSourceViewer { ViewerId = Viewer1, Name = "Viewer One" };
        viewer.ParticipantIds.Add(P2);
        source.Viewers.Add(viewer);

        var report = Build(source, viewerId: Viewer1);

        Assert.Equal(Viewer1, report.SelectedViewerId);
        var rows = report.Tables[0].Rows;
        Assert.Equal(5, rows.Count);
        Assert.Equal("L002", rows[4].Cells[0].Text);
    }

    [Fact]
    public void Build_UnknownViewerId_FallsBackToNormalView()
    {
        var source = Source(viewers: true);
        source.Tests.Add(PlainTest());
        source.Participants.Add(WithResult(Participant(P1, "L001"), SampleA, "Pos"));
        var viewer = new TabulationSourceViewer { ViewerId = Viewer1, Name = "Viewer One" };
        source.Viewers.Add(viewer);

        var report = Build(source, viewerId: Guid.NewGuid());

        Assert.Equal(Guid.Empty, report.SelectedViewerId);
        Assert.Equal(5, report.Tables[0].Rows.Count);
    }

    [Fact]
    public void Build_IntendedResultsOnly_OffersNoFiltersAndShowsNoParticipantRows()
    {
        var source = Source(intendedOnly: true, single: true, viewers: true, hasRatings: true);
        source.Tests.Add(PlainTest());
        source.Participants.Add(WithResult(Participant(P1, "L001"), SampleA, "Pos"));
        source.Viewers.Add(new TabulationSourceViewer { ViewerId = Viewer1, Name = "Viewer One" });

        var report = Build(source, participantId: P1, viewerId: Viewer1);

        Assert.Empty(report.ParticipantOptions);
        Assert.Empty(report.ViewerOptions);
        Assert.Equal(4, report.Tables[0].Rows.Count);
        Assert.Empty(report.Print.Participants);
        // Ratings table: header + the four statistic rows only.
        Assert.Equal(5, report.Ratings!.Rows.Count);
    }

    [Fact]
    public void Build_OnlyTestsWithSamplesAndAtLeastOneItemAreRendered()
    {
        var source = Source();
        var noSamples = new TabulationSourceTest { TestId = Guid.NewGuid(), Name = "No samples" };
        noSamples.ResultItems.Add(new(Guid.NewGuid(), "Result"));
        var noItems = new TabulationSourceTest { TestId = Guid.NewGuid(), Name = "No items" };
        noItems.Samples.Add(new(SampleA, null, "S1", ""));
        var methodOnly = new TabulationSourceTest { TestId = Guid.NewGuid(), Name = "Method only" };
        methodOnly.Samples.Add(new(SampleA, null, "S1", ""));
        methodOnly.MethodItems.Add(new(Method1, "Method"));
        var categoryOnly = new TabulationSourceTest { TestId = Guid.NewGuid(), Name = "Category only" };
        categoryOnly.Samples.Add(new(SampleA, null, "S1", ""));
        categoryOnly.CategoryItems.Add(new(Category1, "Clinical"));
        source.Tests.AddRange([noSamples, noItems, methodOnly, categoryOnly, PlainTest()]);

        var report = Build(source);

        Assert.Equal(["Method only", "Category only", "Culture"], report.Tables.Select(t => t.Caption));
        Assert.Equal(["Method only", "Category only", "Culture"], report.Print.Tests.Select(t => t.Name));
    }

    [Fact]
    public void Build_NonAssessedScheme_BuildsLegacyHeaderIntendedAndParticipantRows()
    {
        var source = Source();
        source.Tests.Add(PlainTest());
        source.IntendedMethodResults[Method1] = "ELISA";
        source.IntendedResultItemResults[(SampleA, Result1)] = ("Pos", "");
        source.IntendedResultItemResults[(SampleB, Result1)] = ("1", "5");
        var withResults = Participant(P1, "L001");
        withResults.MethodResults[Method1] = "PCR";
        withResults.ResultItemResults[(SampleA, Result1)] = "Pos";
        withResults.ResultItemResults[(SampleB, Result1)] = "";
        source.Participants.Add(withResults);
        source.Participants.Add(Participant(P2, "L002", "No samples received"));

        var table = Assert.Single(Build(source).Tables);

        Assert.Equal("Culture", table.Caption);
        Assert.Null(table.AssessmentTestId);
        Assert.Equal(6, table.Rows.Count);
        Assert.Equal([true, true, false, false, false, false], table.Rows.Select(r => r.IsHeader));

        Assert.Equal(
            [("Culture", 1, 2, "Header1", false), ("Method", 1, 2, "Header2", false), ("S1", 1, 1, "Header1", false), ("S2", 1, 1, "Header1", false)],
            table.Rows[0].Cells.Select(C));
        Assert.Equal([("Result", 1, 1, "Header2", false), ("Result", 1, 1, "Header2", false)], table.Rows[1].Cells.Select(C));
        Assert.Equal(
            [("Intended", 1, 2, "Cell", false), ("", 1, 1, "Plain", false), ("Positive", 1, 1, "Cell", false), ("Negative", 1, 1, "Cell", false)],
            table.Rows[2].Cells.Select(C));
        Assert.Equal(["ELISA", "Pos", "1 to 5"], Texts(table.Rows[3]));
        Assert.Equal(
            [("L001", 1, 1, "Cell", false), ("PCR", 1, 1, "Cell", false), ("Pos", 1, 1, "Cell", false), ("", 1, 1, "Cell", true)],
            table.Rows[4].Cells.Select(C));

        // Legacy adds the (absent) assessment columns to the nil-return span: 1 method + (1 + 0 + 2) * 2 samples.
        Assert.Equal([("L002", 1, 1, "Cell", false), ("No samples received", 7, 1, "Cell", true)], table.Rows[5].Cells.Select(C));
    }

    [Fact]
    public void Build_MissingIntendedValues_AreEmptyCells()
    {
        var source = Source();
        source.Tests.Add(PlainTest());

        var table = Build(source).Tables[0];

        Assert.Equal(["", "", ""], Texts(table.Rows[3]));
    }

    [Fact]
    public void Build_NonAssessedTestWithoutResultItems_HasNoSampleHeaderCells()
    {
        var source = Source();
        var test = new TabulationSourceTest { TestId = TestId, Name = "Methods" };
        test.Samples.Add(new(SampleA, null, "S1", "Desc"));
        test.MethodItems.Add(new(Method1, "Method"));
        source.Tests.Add(test);

        var table = Build(source).Tables[0];

        Assert.Equal(["Methods", "Method"], Texts(table.Rows[0]));
        Assert.Empty(table.Rows[1].Cells);
        // A zero span is drawn as 1, as ASP.NET omitted a zero ColumnSpan.
        Assert.Equal(1, table.Rows[2].Cells[2].ColSpan);
    }

    [Fact]
    public void Build_AssessedTestWithoutResultOrCategoryItems_HasNoSampleHeaderCells()
    {
        var source = Source(assess: true);
        var test = new TabulationSourceTest { TestId = TestId, Name = "Methods", IsAssessmentSubmitted = true };
        test.Samples.Add(new(SampleA, null, "S1", "Desc"));
        test.MethodItems.Add(new(Method1, "Method"));
        source.Tests.Add(test);

        var table = Build(source).Tables[0];

        Assert.Equal(["Methods", "Method"], Texts(table.Rows[0]));
        Assert.Equal(["Assessment comment", "Overall Score"], Texts(table.Rows[1]));
        Assert.Equal(2, table.Rows[2].Cells[2].ColSpan);
    }

    [Fact]
    public void Build_AssessedScheme_BuildsAssessmentHeaderIntendedRowsAndLink()
    {
        var source = Source(assess: true);
        source.Tests.Add(AssessedTest());
        source.IntendedMethodResults[Method1] = "ELISA";
        source.IntendedResultItemResults[(SampleA, Result1)] = ("Abortus", "");

        var table = Build(source).Tables[0];

        Assert.Equal(TestId, table.AssessmentTestId);
        Assert.Equal(
            [("Clinical", 1, 2, "Header1", false), ("Method", 1, 2, "Header2", false), ("S1", 5, 1, "Header1", false)],
            table.Rows[0].Cells.Select(C));
        Assert.Equal(["Identity", "Clinical", "Pathology", "Assessment comment", "Overall Score"], Texts(table.Rows[1]));
        Assert.Equal(("Brucella abortus", 5), (table.Rows[2].Cells[2].Text, table.Rows[2].Cells[2].ColSpan));
        Assert.Equal(["ELISA", "Abortus", "", "", "", ""], Texts(table.Rows[3]));
    }

    [Fact]
    public void Build_AssessedSchemeWithAssessmentNotSubmitted_HasNoAssessmentLink()
    {
        var source = Source(assess: true);
        source.Tests.Add(AssessedTest(submitted: false));

        Assert.Null(Build(source).Tables[0].AssessmentTestId);
    }

    [Fact]
    public void Build_AssessedScheme_ScoredParticipantShowsCategoryCriteriaCommentAndScore()
    {
        var source = Source(assess: true);
        source.Tests.Add(AssessedTest());
        var participant = WithResult(Participant(P1, "L001"), SampleA, "Abortus");
        participant.MethodResults[Method1] = "PCR";
        var category = new TabulationSourceCategoryResult { SampleTestId = SampleTestA, Score = 3, IsAssessmentSubmitted = true };
        category.Criteria.AddRange(["Correct ID", "Good notes "]);
        participant.CategoryResults[(SampleA, Category1)] = category;
        participant.Assessments[(SampleA, TestId)] = Assessment(4, Submitted, assessmentSubmitted: Submitted, comment: "Well done");
        source.Participants.Add(participant);

        var row = Build(source).Tables[0].Rows[4];

        Assert.Equal(
            [("L001", 1, 1, "Cell", false), ("PCR", 1, 1, "Cell", false), ("Abortus", 1, 1, "Cell", false),
             ("Correct ID, Good notes [3/5]", 1, 1, "Cell", false), ("", 1, 1, "Cell", true),
             ("Well done", 1, 1, "Cell", false), ("Good [4/5]", 1, 1, "Cell", false)],
            row.Cells.Select(C));
    }

    [Fact]
    public void Build_DraftCategoryText_RequiresSubmittedCategoryAndMatchingSampleTest()
    {
        var source = Source(assess: true);
        source.Tests.Add(AssessedTest());
        var participant = WithResult(Participant(P1, "L001"), SampleA, "Abortus");
        var notSubmitted = new TabulationSourceCategoryResult { SampleTestId = SampleTestA, Score = 3, IsAssessmentSubmitted = false };
        notSubmitted.Criteria.Add("Hidden");
        var otherSampleTest = new TabulationSourceCategoryResult { SampleTestId = Guid.NewGuid(), Score = 5, IsAssessmentSubmitted = true };
        otherSampleTest.Criteria.Add("Other");
        participant.CategoryResults[(SampleA, Category1)] = notSubmitted;
        participant.CategoryResults[(SampleA, Category2)] = otherSampleTest;
        participant.Assessments[(SampleA, TestId)] = Assessment(2, Submitted, assessmentSubmitted: Submitted);
        source.Participants.Add(participant);

        var row = Build(source).Tables[0].Rows[4];

        Assert.Equal(["L001", "", "Abortus", "", "", "", "Borderline Fail [2/5]"], Texts(row));
    }

    [Fact]
    public void Build_PublishedCategoryText_IgnoresSubmissionFlagAndShowsWithoutSampleTest()
    {
        var source = Source(published: true, assess: true);
        source.Tests.Add(AssessedTest());
        var participant = WithResult(Participant(P1, "L001"), SampleA, "Abortus");
        var category = new TabulationSourceCategoryResult { Score = 5, IsAssessmentSubmitted = false };
        category.Criteria.Add("Complete");
        participant.CategoryResults[(SampleA, Category1)] = category;
        participant.Assessments[(SampleA, TestId)] = Assessment(5, Submitted, comment: "Excellent work");
        source.Participants.Add(participant);

        var row = Build(source).Tables[0].Rows[4];

        Assert.Equal(["L001", "", "Abortus", "Complete [5/5]", "", "Excellent work", "Excellent [5/5]"], Texts(row));
    }

    [Fact]
    public void Build_ScoredParticipantWithoutAnyCategoryResults_OmitsTheCategoryCells()
    {
        var source = Source(assess: true);
        source.Tests.Add(AssessedTest());
        var participant = WithResult(Participant(P1, "L001"), SampleA, "Abortus");
        participant.Assessments[(SampleA, TestId)] = Assessment(3, Submitted, assessmentSubmitted: Submitted, comment: "OK");
        source.Participants.Add(participant);

        var row = Build(source).Tables[0].Rows[4];

        Assert.Equal(["L001", "", "Abortus", "OK", "Pass [3/5]"], Texts(row));
    }

    [Fact]
    public void Build_UnscoredParticipant_ShowsOneScoreCellSpanningCategoriesAndAssessmentColumns()
    {
        var source = Source(assess: true);
        source.Tests.Add(AssessedTest());
        var participant = WithResult(Participant(P1, "L001"), SampleA, "Abortus");
        participant.Assessments[(SampleA, TestId)] = Assessment(3, Submitted, comment: "Not yet visible");
        source.Participants.Add(participant);

        var row = Build(source).Tables[0].Rows[4];

        Assert.Equal(("Submitted", 4, 1, "Cell", false), C(row.Cells[^1]));
        Assert.Equal(4, row.Cells.Count);
    }

    [Fact]
    public void Build_AssessedParticipantWithoutResults_ShowsNilReasonOverMethodsAndScoreOnlyOutsideOneToFive()
    {
        var source = Source(assess: true);
        source.Tests.Add(AssessedTest());
        var notSubmitted = Participant(P1, "L001", "No return");
        notSubmitted.Assessments[(SampleA, TestId)] = Assessment(0, nonSubmission: Submitted);
        var scored = Participant(P2, "L002", "Lost");
        scored.Assessments[(SampleA, TestId)] = Assessment(3, Submitted, assessmentSubmitted: Submitted);
        source.Participants.AddRange([notSubmitted, scored]);

        var rows = Build(source).Tables[0].Rows;

        Assert.Equal(
            [("L001", 1, 1, "Cell", false), ("No return", 1, 1, "Cell", true), ("Not Submitted", 5, 1, "Cell", false)],
            rows[4].Cells.Select(C));
        Assert.Equal([("L002", 1, 1, "Cell", false), ("Lost", 1, 1, "Cell", true)], rows[5].Cells.Select(C));
    }

    [Fact]
    public void Build_AssessedParticipantWithoutResultsAndNoMethodItems_ShowsOnlyTheScoreCell()
    {
        var source = Source(assess: true);
        var test = new TabulationSourceTest { TestId = TestId, Name = "Clinical", IsAssessmentSubmitted = true };
        test.Samples.Add(new(SampleA, SampleTestA, "S1", "Desc"));
        test.ResultItems.Add(new(Result1, "Identity"));
        source.Tests.Add(test);
        source.Participants.Add(Participant(P1, "L001", "Nil"));

        var row = Build(source).Tables[0].Rows[4];

        // No assessment row at all is shown as Unaccounted (legacy dereferenced it and failed).
        Assert.Equal([("L001", 1, 1, "Cell", false), ("Unaccounted", 3, 1, "Cell", false)], row.Cells.Select(C));
    }

    public static TheoryData<bool, TabulationSourceAssessment, string> ScoreTexts() => new()
    {
        { false, Assessment(3, Submitted), "Submitted" },
        { false, Assessment(1, Submitted, assessmentSubmitted: Submitted), "Fail [1/5]" },
        { false, Assessment(2, Submitted, assessmentSubmitted: Submitted), "Borderline Fail [2/5]" },
        { false, Assessment(3, Submitted, assessmentSubmitted: Submitted), "Pass [3/5]" },
        { false, Assessment(4, Submitted, assessmentSubmitted: Submitted), "Good [4/5]" },
        { false, Assessment(5, Submitted, assessmentSubmitted: Submitted), "Excellent [5/5]" },
        { false, Assessment(0, Submitted, assessmentSubmitted: Submitted), "Not Assessed" },
        { false, Assessment(6, Submitted, assessmentSubmitted: Submitted), "Not Assessed" },
        { false, Assessment(0, nonSubmission: Submitted), "Not Submitted" },
        { false, Assessment(-1, nonCapable: Submitted), "Not In Repertoire" },
        { false, Assessment(0), "Unaccounted" },
        { true, Assessment(3, Submitted), "Pass [3/5]" },
        { true, Assessment(0, Submitted), "Not Assessed" },
        { true, Assessment(0, nonSubmission: Submitted), "Not Submitted" },
        { true, Assessment(-1, nonCapable: Submitted), "Not In Repertoire" },
        { true, Assessment(0), "Unaccounted" },
    };

    [Theory]
    [MemberData(nameof(ScoreTexts))]
    public void Build_OverallScoreText_FollowsLegacyAssessmentScoreTexts(bool published, TabulationSourceAssessment assessment, string expected)
    {
        var source = Source(published: published, assess: true);
        source.Tests.Add(AssessedTest());
        var participant = WithResult(Participant(P1, "L001"), SampleA, "Abortus");
        participant.Assessments[(SampleA, TestId)] = assessment;
        source.Participants.Add(participant);

        var report = Build(source);

        Assert.Equal(expected, report.Tables[0].Rows[4].Cells[^1].Text);
        Assert.Equal(expected, report.Print.Tests[0].SampleColumns[0].ResultColumns[^1].Results[0].Result);
    }

    [Fact]
    public void Build_PublishedUnsubmittedAssessment_IsScoredAndShowsTheComment()
    {
        var source = Source(published: true, assess: true);
        source.Tests.Add(AssessedTest());
        var participant = WithResult(Participant(P1, "L001"), SampleA, "Abortus");
        participant.Assessments[(SampleA, TestId)] = Assessment(4, Submitted, comment: "Published comment");
        source.Participants.Add(participant);

        var row = Build(source).Tables[0].Rows[4];

        Assert.Equal(["L001", "", "Abortus", "Published comment", "Good [4/5]"], Texts(row));
    }

    [Fact]
    public void Build_NoRatings_RatingsTableIsNullAndPrintHasNoRatingColumns()
    {
        var source = Source();
        source.Tests.Add(PlainTest());

        var report = Build(source);

        Assert.Null(report.Ratings);
        Assert.Empty(report.Print.RatingColumns);
    }

    private static TabulationSource RatingsSource()
    {
        var source = Source(hasRatings: true, viewers: true);
        source.Tests.Add(PlainTest());
        var other = new TabulationSourceTest { TestId = Guid.NewGuid(), Name = "Other" };
        // S10 sorts ordinally between S1 and S2; SampleA appears again and is de-duplicated.
        other.Samples.Add(new(SampleA, null, "S1", ""));
        other.Samples.Add(new(Guid.Parse("20000000-0000-0000-0000-00000000000c"), null, "S10", ""));
        other.ResultItems.Add(new(Result1, "Result"));
        source.Tests.Add(other);

        var p1 = WithResult(Participant(P1, "L001"), SampleA, "x");
        p1.SampleRatings[SampleA] = "2";
        p1.SampleRatings[SampleB] = "Not Scored";
        var p2 = WithResult(Participant(P2, "L002"), SampleA, "x");
        p2.SampleRatings[SampleA] = "1";
        p2.SampleRatings[SampleB] = "2";
        var p3 = WithResult(Participant(P3, "L003"), SampleA, "x");
        p3.SampleRatings[SampleA] = "Not Tested";
        p3.SampleRatings[SampleB] = "2";
        // Method results only - shown in the table but ignored by the statistics.
        var p4 = Participant(P4, "L004");
        p4.MethodResults[Method1] = "PCR";
        p4.SampleRatings[SampleA] = "2";
        var p5 = Participant(Guid.NewGuid(), "L005", "Nil");
        source.Participants.AddRange([p1, p2, p3, p4, p5]);
        return source;
    }

    [Fact]
    public void Build_Ratings_DeduplicatesAndOrdinallySortsSamplesAndBuildsParticipantRows()
    {
        var ratings = Build(RatingsSource()).Ratings!;

        Assert.Equal("QAU Ratings", ratings.Caption);
        Assert.Null(ratings.AssessmentTestId);
        Assert.Equal(
            [("QAU Ratings", 1, 1, "Header1", false), ("S1", 1, 1, "Header2", false), ("S10", 1, 1, "Header2", false), ("S2", 1, 1, "Header2", false)],
            ratings.Rows[0].Cells.Select(C));
        Assert.True(ratings.Rows[0].IsHeader);
        Assert.Equal(["L001", "2", "", "Not Scored"], Texts(ratings.Rows[1]));
        Assert.Equal(["L002", "1", "", "2"], Texts(ratings.Rows[2]));
        Assert.Equal(["L003", "Not Tested", "", "2"], Texts(ratings.Rows[3]));
        Assert.Equal(["L004", "2", "", ""], Texts(ratings.Rows[4]));
        Assert.Equal([("L005", 1, 1, "Cell", false), ("", 1, 1, "Cell", true), ("", 1, 1, "Cell", true), ("", 1, 1, "Cell", true)], ratings.Rows[5].Cells.Select(C));
    }

    [Fact]
    public void Build_Ratings_StatisticsFollowLegacyArithmetic()
    {
        var rows = Build(RatingsSource()).Ratings!.Rows;

        // S1: mean of 2 and 1 (Not Tested ignored, L004 has no result items); S10: no ratings; S2: Not Scored present.
        Assert.Equal(["Mean score of all laboratories: ", "1.5", "0.0", "Not Scored"], Texts(rows[6]));
        Assert.Equal(["No. of labs reporting intended result: ", "1", "0", "Not Scored"], Texts(rows[7]));
        Assert.Equal(["No. of Participants: ", "2", "0", "3"], Texts(rows[8]));
        Assert.Equal(["Percent Correct:", "50.0%", "0.0%", "Not Scored"], Texts(rows[9]));
        Assert.All(rows.Skip(6), r => Assert.False(r.IsHeader));
    }

    [Fact]
    public void Build_Ratings_StatisticsUseEveryParticipantWhateverTheViewerFilter()
    {
        var source = RatingsSource();
        var viewer = new TabulationSourceViewer { ViewerId = Viewer1, Name = "Viewer One" };
        viewer.ParticipantIds.Add(P3);
        source.Viewers.Add(viewer);

        var ratings = Build(source, viewerId: Viewer1).Ratings!;

        Assert.Equal(6, ratings.Rows.Count);
        Assert.Equal("L003", ratings.Rows[1].Cells[0].Text);
        Assert.Equal(["Mean score of all laboratories: ", "1.5", "0.0", "Not Scored"], Texts(ratings.Rows[2]));
    }

    [Fact]
    public void Build_Ratings_MeanIsRoundedToOneDecimalAndNonNumericRatingsIgnored()
    {
        var source = Source(hasRatings: true);
        source.Tests.Add(PlainTest());
        foreach (var (rating, index) in new[] { "2", "2", "-1", "abc" }.Select((r, i) => (r, i)))
        {
            var participant = WithResult(Participant(Guid.NewGuid(), $"L00{index}"), SampleA, "x");
            participant.SampleRatings[SampleA] = rating;
            source.Participants.Add(participant);
        }

        var rows = Build(source).Ratings!.Rows;

        Assert.Equal("1.0", rows[5].Cells[1].Text);
        Assert.Equal("2", rows[6].Cells[1].Text);
        Assert.Equal("3", rows[7].Cells[1].Text);
        Assert.Equal("66.7%", rows[8].Cells[1].Text);
    }

    [Fact]
    public void Build_PrintModel_NonAssessedSchemeParticipantsMethodAndResultColumns()
    {
        var source = Source();
        source.Tests.Add(PlainTest());
        source.IntendedMethodResults[Method1] = "ELISA";
        source.IntendedResultItemResults[(SampleB, Result1)] = ("1", "5");
        var withResults = Participant(P1, "L001");
        withResults.MethodResults[Method1] = "PCR";
        withResults.ResultItemResults[(SampleA, Result1)] = "Pos";
        source.Participants.Add(withResults);
        source.Participants.Add(Participant(P2, "L002", "Nil reason"));

        var print = Build(source).Print;

        Assert.Equal([new("L001", true, ""), new("L002", false, "Nil reason")], print.Participants);
        var test = Assert.Single(print.Tests);
        Assert.Equal("Culture", test.Name);
        var method = Assert.Single(test.MethodColumns);
        Assert.Equal(("Method", "ELISA"), (method.HeaderText, method.IntendedResult));
        Assert.Equal(["PCR", ""], method.Results);
        Assert.Equal([("S1", "Positive"), ("S2", "Negative")], test.SampleColumns.Select(s => (s.SampleNumber, s.TabulationDescription)));
        var s1 = Assert.Single(test.SampleColumns[0].ResultColumns);
        Assert.Equal(("Result", ""), (s1.HeaderText, s1.IntendedResult));
        Assert.Equal([new("Pos", 1), new("", 1)], s1.Results);
        Assert.Equal("1 to 5", test.SampleColumns[1].ResultColumns[0].IntendedResult);
    }

    [Fact]
    public void Build_PrintModel_AssessedSchemeSpansFollowLegacyWriteResultColumns()
    {
        var source = Source(assess: true);
        source.Tests.Add(AssessedTest());
        var scored = WithResult(Participant(P1, "L001"), SampleA, "Abortus");
        var category = new TabulationSourceCategoryResult { SampleTestId = SampleTestA, Score = 3, IsAssessmentSubmitted = true };
        category.Criteria.Add("Correct");
        scored.CategoryResults[(SampleA, Category1)] = category;
        scored.Assessments[(SampleA, TestId)] = Assessment(4, Submitted, assessmentSubmitted: Submitted, comment: "Fine");
        var unscored = Participant(P2, "L002", "Nil");
        unscored.Assessments[(SampleA, TestId)] = Assessment(0, nonSubmission: Submitted);
        source.Participants.AddRange([scored, unscored]);

        var print = Build(source).Print;

        Assert.All(print.Participants, p => Assert.True(p.HasResults));
        var columns = print.Tests[0].SampleColumns[0].ResultColumns;
        Assert.Equal(["Identity", "Clinical", "Pathology", "Assessment Comment", "Overall score"], columns.Select(c => c.HeaderText));
        Assert.All(columns.Skip(1), c => Assert.Equal(string.Empty, c.IntendedResult));
        Assert.Equal([new("Abortus", 1), new("", 1)], columns[0].Results);
        Assert.Equal([new("Correct [3/5]", 1), new("Not Submitted", 4)], columns[1].Results);
        Assert.Equal([new("", 1), new("Not Submitted", 0)], columns[2].Results);
        Assert.Equal([new("Fine", 1), new("Not Submitted", 0)], columns[3].Results);
        Assert.Equal([new("Good [4/5]", 1), new("Not Submitted", 0)], columns[4].Results);
    }

    [Fact]
    public void Build_PrintModel_AssessedSchemeWithoutCategoriesCommentColumnCarriesTheSpan()
    {
        var source = Source(assess: true);
        var test = new TabulationSourceTest { TestId = TestId, Name = "Clinical", IsAssessmentSubmitted = true };
        test.Samples.Add(new(SampleA, SampleTestA, "S1", "Desc"));
        test.ResultItems.Add(new(Result1, "Identity"));
        source.Tests.Add(test);
        var unscored = WithResult(Participant(P1, "L001"), SampleA, "Abortus");
        unscored.Assessments[(SampleA, TestId)] = Assessment(-1, nonCapable: Submitted);
        source.Participants.Add(unscored);

        var columns = Build(source).Print.Tests[0].SampleColumns[0].ResultColumns;

        Assert.Equal(["Identity", "Assessment Comment", "Overall score"], columns.Select(c => c.HeaderText));
        Assert.Equal(new("Not In Repertoire", 2), columns[1].Results[0]);
        Assert.Equal(new("Not In Repertoire", 0), columns[2].Results[0]);
    }

    [Fact]
    public void Build_PrintRatingColumns_UseNonBreakingSpaceForParticipantsWithoutRatings()
    {
        var print = Build(RatingsSource()).Print;

        Assert.Equal(["S1", "S10", "S2"], print.RatingColumns.Select(c => c.SampleNumber));
        var s1 = print.RatingColumns[0];
        Assert.Equal(["2", "1", "Not Tested", "2", "\u00a0"], s1.Ratings);
        Assert.Equal(("1.5", "50.0%", "2", "1"), (s1.SampleMean, s1.SamplePassRate, s1.NumberOfParticipants, s1.NumberReportingIntendedResult));
        Assert.Equal(["", "", "", "", "\u00a0"], print.RatingColumns[1].Ratings);
        var s2 = print.RatingColumns[2];
        Assert.Equal(("Not Scored", "Not Scored", "3", "Not Scored"), (s2.SampleMean, s2.SamplePassRate, s2.NumberOfParticipants, s2.NumberReportingIntendedResult));
        Assert.Equal([true, true, true, true, false], print.Participants.Select(p => p.HasResults));
    }
}
