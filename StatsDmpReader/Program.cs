using System;
using System.Collections.Generic;
using GameStatsProcessor;

string filePath = @"C:\Games\CnCNet\RedAlert1_Online\stats.dmp";

// Correct countable stat arrays for Red Alert 1
var countableTags = new List<string>
{
    "BLC", "VSK", "BLK", "PLK", "UNK", "INK", "VSL",
    "BLL", "PLL", "UNL", "INL", "VSB", "BLB", "PLB",
    "UNB", "INB"
};

var parser = new StatsDmpProcessor();
var result = parser.ProcessStatsDmp(filePath, countableTags);

if (result == null)
{
    Console.WriteLine("Failed to read stats.dmp");
    return;
}

foreach (var entry in result)
{
    Console.Write($"{entry.Key}: ");

    if (entry.Value.ContainsKey("counts"))
    {
        Console.WriteLine();
        var counts = (Dictionary<int, uint>)entry.Value["counts"];

        foreach (var c in counts)
            Console.WriteLine($"    Index {c.Key}: {c.Value}");

        continue;
    }

    Console.WriteLine(entry.Value["value"]);
}