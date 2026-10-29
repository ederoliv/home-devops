using HomeDevops.Models;

namespace HomeDevops.Tests;

public class BreadcrumbFocusTests
{
    [Fact]
    public void Removing_the_open_item_clears_the_current_item()
    {
        var project = new Project();
        var item = new Item { Title = "A" };
        project.Items.Add(item);
        project.CurrentItem = item;

        project.Items.Remove(item);

        Assert.Null(project.CurrentItem);
    }

    [Fact]
    public void Removing_another_item_keeps_the_current_item()
    {
        var project = new Project();
        var open = new Item { Title = "A" };
        var other = new Item { Title = "B" };
        project.Items.Add(open);
        project.Items.Add(other);
        project.CurrentItem = open;

        project.Items.Remove(other);

        Assert.Same(open, project.CurrentItem);
    }

    [Fact]
    public void Leaving_an_item_clears_its_current_task()
    {
        var project = new Project();
        var item = new Item();
        project.Items.Add(item);
        project.CurrentItem = item;
        item.AddTask("Um");
        Assert.NotNull(item.CurrentTask);

        project.CurrentItem = null;

        Assert.Null(item.CurrentTask);
    }

    [Fact]
    public void Adding_or_checking_a_task_makes_it_current_and_removing_it_clears_the_focus()
    {
        var item = new Item();
        var one = item.AddTask("Um");
        var two = item.AddTask("Dois");
        Assert.Same(two, item.CurrentTask);

        one.Done = true;
        Assert.Same(one, item.CurrentTask);

        item.RemoveTask(one);
        Assert.Null(item.CurrentTask);
    }

    [Fact]
    public void Focus_state_is_not_written_to_the_file()
    {
        var project = new Project { Name = "P" };
        var item = new Item { Title = "I" };
        project.Items.Add(item);
        project.CurrentItem = item;
        item.AddTask("T");

        var json = System.Text.Json.JsonSerializer.Serialize(project, HdpJson.Options);

        Assert.DoesNotContain("currentItem", json);
        Assert.DoesNotContain("currentTask", json);
    }
}
