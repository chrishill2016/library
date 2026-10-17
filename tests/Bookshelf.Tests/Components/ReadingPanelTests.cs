using Bookshelf.Features.Books;
using Bookshelf.Models;
using Bookshelf.Tests.Fakes;
using Bunit;
using Microsoft.Extensions.DependencyInjection;

namespace Bookshelf.Tests.Components;

public class ReadingPanelTests : ComponentTestBase
{
    private async Task<IRenderedComponent<BookDetailsPage>> RenderBookAsync()
    {
        await SignInAsync();
        await Store.PutBookAsync(TestBooks.Make("b1", "Emma"));
        var cut = Render<BookDetailsPage>(p => p.Add(x => x.Id, "b1"));
        cut.WaitForElement("[data-testid=my-reading]");
        return cut;
    }

    private Reading Mine => Store.Readings["b1_chris"];

    [Fact]
    public async Task Hidden_when_nobody_is_signed_in()
    {
        await Store.PutBookAsync(TestBooks.Make("b1", "Emma"));

        var cut = Render<BookDetailsPage>(p => p.Add(x => x.Id, "b1"));

        cut.WaitForAssertion(() => Assert.Contains("Emma", cut.Markup));
        Assert.Empty(cut.FindAll("[data-testid=my-reading]"));
    }

    [Fact]
    public async Task Tapping_a_star_rates_and_tapping_it_again_clears()
    {
        var cut = await RenderBookAsync();

        cut.Find("[data-testid=star-4]").Click();
        cut.WaitForAssertion(() => Assert.Equal(4, Mine.Rating));
        Assert.Equal("true", cut.Find("[data-testid=star-4]").GetAttribute("aria-pressed"));

        cut.Find("[data-testid=star-4]").Click();
        cut.WaitForAssertion(() => Assert.Null(Mine.Rating));
    }

    [Fact]
    public async Task Status_buttons_set_and_clear_the_status()
    {
        var cut = await RenderBookAsync();

        cut.Find("[data-testid=status-reading]").Click();
        cut.WaitForAssertion(() => Assert.Equal(ReadingStatus.Reading, Mine.Status));

        cut.Find("[data-testid=status-want]").Click();
        cut.WaitForAssertion(() => Assert.Equal(ReadingStatus.WantToRead, Mine.Status));

        cut.Find("[data-testid=status-want]").Click();
        cut.WaitForAssertion(() => Assert.Null(Mine.Status));
    }

    [Fact]
    public async Task Finished_today_adds_todays_date_and_marks_read()
    {
        var cut = await RenderBookAsync();

        cut.Find("[data-testid=add-read]").Click();
        cut.Find("[data-testid=read-today]").Click();

        cut.WaitForAssertion(() => Assert.Equal([ReadDate.On(DateOnly.FromDateTime(Time.GetLocalNow().DateTime))], Mine.Reads));
        Assert.Equal(ReadingStatus.Read, Mine.Status);
        cut.WaitForAssertion(() => Assert.Contains("2 January 2026", cut.Find("[data-testid=my-reads]").TextContent));
        Assert.Empty(cut.FindAll("[data-testid=add-read-form]"));
    }

    [Fact]
    public async Task A_year_on_its_own_can_be_recorded_and_removed()
    {
        var cut = await RenderBookAsync();
        cut.Find("[data-testid=add-read]").Click();

        cut.Find("input[data-testid=read-year]").Change("2015");
        cut.Find("[data-testid=read-add]").Click();

        cut.WaitForAssertion(() => Assert.Equal([new ReadDate(2015)], Mine.Reads));
        cut.WaitForAssertion(() => Assert.Contains("2015", cut.Find("[data-testid=my-reads]").TextContent));

        cut.Find("[data-testid=remove-read]").Click();
        cut.WaitForAssertion(() => Assert.Empty(Mine.Reads));
    }

    [Fact]
    public async Task Adding_without_a_year_explains_what_is_needed()
    {
        var cut = await RenderBookAsync();
        cut.Find("[data-testid=add-read]").Click();

        cut.Find("input[data-testid=read-year]").Change("");
        cut.Find("[data-testid=read-add]").Click();

        Assert.Contains("at least the year", cut.Find("[data-testid=read-error]").TextContent);
        Assert.Empty(Store.Readings);
    }

    [Fact]
    public async Task Review_is_saved_with_the_save_button()
    {
        var cut = await RenderBookAsync();
        Assert.Empty(cut.FindAll("[data-testid=save-review]"));

        cut.Find("textarea[data-testid=review]").Input("A delight from start to finish.");
        cut.Find("[data-testid=save-review]").Click();

        cut.WaitForAssertion(() => Assert.Equal("A delight from start to finish.", Mine.Review));
        cut.WaitForAssertion(() => Assert.Empty(cut.FindAll("[data-testid=save-review]")));
    }

    [Fact]
    public async Task Shows_the_other_persons_reading_by_name()
    {
        await Store.PutReadingAsync(TestReadings.Make("b1", "wife", rating: 5, status: ReadingStatus.Read,
            review: "Loved it.", name: "Sarah", reads: new ReadDate(2019, 6)));

        var cut = await RenderBookAsync();

        var other = cut.WaitForElement("[data-testid=other-reading]");
        Assert.Contains("Sarah", other.TextContent);
        Assert.Contains("Loved it.", other.TextContent);
        Assert.Contains("June 2019", other.TextContent);
        Assert.Equal("5 out of 5 stars", other.QuerySelector(".star-rating")!.GetAttribute("aria-label"));
    }

    [Fact]
    public async Task Panel_updates_when_a_sync_brings_new_readings()
    {
        var cut = await RenderBookAsync();
        Assert.Empty(cut.FindAll("[data-testid=other-reading]"));

        await Store.PutReadingAsync(TestReadings.Make("b1", "wife", rating: 2, name: "Sarah"));
        Services.GetRequiredService<Bookshelf.Features.ReadingLog.ReadingService>().Invalidate();

        cut.WaitForAssertion(() => Assert.Single(cut.FindAll("[data-testid=other-reading]")));
    }
}
