using HomeDevops.Models;

namespace HomeDevops.Tests;

public class ItemRulesTests
{
    private static Item ItemWithTasks(int count)
    {
        var item = new Item { Title = "Login" };
        for (var i = 0; i < count; i++)
        {
            item.AddTask($"t{i}");
        }

        return item;
    }

    [Fact]
    public void Checking_the_first_task_moves_a_todo_item_to_doing()
    {
        var item = ItemWithTasks(2);

        item.Tasks[0].Done = true;

        Assert.Equal(ItemState.Doing, item.State);
    }

    [Fact]
    public void Checking_every_task_completes_the_item()
    {
        var item = ItemWithTasks(2);

        item.Tasks[0].Done = true;
        item.Tasks[1].Done = true;

        Assert.Equal(ItemState.Done, item.State);
    }

    [Fact]
    public void Unchecking_a_task_of_a_done_item_goes_back_to_doing()
    {
        var item = ItemWithTasks(2);
        item.Tasks[0].Done = true;
        item.Tasks[1].Done = true;

        item.Tasks[1].Done = false;

        Assert.Equal(ItemState.Doing, item.State);
    }

    [Fact]
    public void Unchecking_everything_never_goes_back_to_todo()
    {
        var item = ItemWithTasks(1);
        item.Tasks[0].Done = true;
        item.Tasks[0].Done = false;

        Assert.Equal(ItemState.Doing, item.State);
    }

    [Fact]
    public void Adding_a_task_to_a_done_item_reopens_it()
    {
        var item = ItemWithTasks(1);
        item.Tasks[0].Done = true;
        Assert.Equal(ItemState.Done, item.State);

        item.AddTask("mais uma");

        Assert.Equal(ItemState.Doing, item.State);
    }

    [Fact]
    public void Removing_a_task_does_not_recompute_the_state()
    {
        var item = ItemWithTasks(2);
        item.Tasks[0].Done = true;
        Assert.Equal(ItemState.Doing, item.State);

        item.RemoveTask(item.Tasks[1]);

        Assert.Equal(ItemState.Doing, item.State);
    }

    [Fact]
    public void Manual_state_can_be_set_freely()
    {
        var item = ItemWithTasks(1);

        item.State = ItemState.Done;

        Assert.Equal(ItemState.Done, item.State);
    }

    [Fact]
    public void Submitting_a_task_uses_and_clears_the_input_and_ignores_blanks()
    {
        var item = new Item();
        item.NewTaskTitle = "   ";
        item.SubmitTaskCommand.Execute(null);
        Assert.Empty(item.Tasks);

        item.NewTaskTitle = "  escrever testes ";
        item.SubmitTaskCommand.Execute(null);

        Assert.Equal("escrever testes", Assert.Single(item.Tasks).Title);
        Assert.Equal("", item.NewTaskTitle);
    }

    [Fact]
    public void Project_statistics_follow_the_items()
    {
        var project = new Project { Name = "P" };
        var feature = new Item { Type = ItemType.Feature, State = ItemState.Done };
        project.Items.Add(feature);
        project.Items.Add(new Item { Type = ItemType.Feature });
        project.Items.Add(new Item { Type = ItemType.Bug, State = ItemState.Done });
        project.Items.Add(new Item { Type = ItemType.Bug });
        project.Items.Add(new Item { Type = ItemType.Bug });

        Assert.Equal((1, 2, 1, 3), (project.FeaturesDone, project.FeaturesTotal, project.BugsDone, project.BugsTotal));

        feature.State = ItemState.Todo;
        Assert.Equal(0, project.FeaturesDone);
    }

    [Fact]
    public void Changes_bubble_up_to_the_project_but_view_state_does_not()
    {
        var project = new Project();
        var item = new Item();
        project.Items.Add(item);
        var changes = 0;
        project.Changed += (_, _) => changes++;

        item.IsExpanded = true;
        item.NewTaskTitle = "digitando";
        Assert.Equal(0, changes);

        item.AddTask("t").Done = true;
        Assert.True(changes > 0);
    }
}
