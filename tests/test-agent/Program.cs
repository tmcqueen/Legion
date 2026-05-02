using System.Text.Json;
using Legion.Agents.Providers;

namespace Legion.Tests.Agent;

public class Program
{
    public static async Task Main(string[] args)
    {


        var agentOptions = new AgentOptions
        {
            Provider = "MiniMax",
            Model = "MiniMax-M2.1",
            ApiKey = "sk-cp-I6gKJSkTkVVeISMXvrD98ZkCiXA9il-PZ6OJBpAtBooenOFgkTTp_n2X7ig3LXmaz4iB-Pc7WK6rYQ8j2s7PVkMUYR9LpboOXOYTTHhBo-uVrYFerwJtAYE",
            Tools = [
                "read-all-lines",
                "get-file-size",
                "count-lines",
            ]
        };

        var agent = new MiniMaxProvider().CreateAgent(agentOptions);

        // var response = await agent.GetResponseAsync([
        //     new ChatMessage(
        //         ChatRole.User, 
        //         "What tools do you have access? List ALL tools by name. This is non-negotiable. I need to know ALL tools you have access to. If you don't have access to any tools, say 'I don't have access to any tools.'")
        // ]);
        
        // var response = await agent.RunAsync("What tools do you have access? List ALL tools by name. This is non-negotiable. I need to know ALL tools you have access to. If you don't have access to any tools, say 'I don't have access to any tools.'");
        var response = await agent.RunAsync("Read the contents of the file 'test.txt' and return it to me.");

        var options = new JsonSerializerOptions { WriteIndented = true };
        var json = JsonSerializer.Serialize(response, options);
        Console.WriteLine(json); 
        Console.WriteLine("===");

        Console.WriteLine($"Agent response: {response.Text}");


        // response.ToChatResponseUpdates().ToList().ForEach(update =>
        // {
        //     Console.WriteLine($"Update: {update.Text}");
        // });


        // var messages = response.Messages.First();
        // messages.Contents.ToList()
        // .Select(m => m as TextContent)
        // .ToList()
        // .ForEach(m => Console.WriteLine($"Text: {m?.Text ?? "null"}"));

    }
}
