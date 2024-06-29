﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Medallion.Shell.Streams;
using Moq;
using NUnit.Framework;

namespace Medallion.Shell.Tests.Streams;

public abstract class MergedLinesEnumerableTestBase
{
    protected abstract IAsyncEnumerable<string> Create(TextReader reader1, TextReader reader2);

    [Test]
    public async Task TestOneIsEmpty()
    {
        var empty1 = new StringReader(string.Empty);
        var nonEmpty1 = new StringReader("abc\r\ndef\r\nghi\r\njkl");

        var enumerable1 = this.Create(empty1, nonEmpty1);
        var list1 = await enumerable1.ToListAsync();
        list1.SequenceEqual(["abc", "def", "ghi", "jkl"])
            .ShouldEqual(true, string.Join(", ", list1));

        var empty2 = new StringReader(string.Empty);
        var nonEmpty2 = new StringReader("a\nbb\nccc\n");
        var enumerable2 = this.Create(nonEmpty2, empty2);
        var list2 = await enumerable2.ToListAsync();
        list2.SequenceEqual(["a", "bb", "ccc"])
            .ShouldEqual(true, string.Join(", ", list2));
    }

    [Test]
    public async Task TestBothAreEmpty()
    {
        var list = await this.Create(new StringReader(string.Empty), new StringReader(string.Empty)).ToListAsync();
        list.Count.ShouldEqual(0, string.Join(", ", list));
    }

    [Test]
    public async Task TestBothArePopulatedEqualSizes()
    {
        var list = await this.Create(
                new StringReader("a\nbb\nccc"),
                new StringReader("1\r\n22\r\n333")
            )
            .ToListAsync();
        string.Join(", ", list).ShouldEqual("a, 1, bb, 22, ccc, 333");
    }

    [Test]
    public async Task TestBothArePopulatedDifferenceSizes()
    {
        var lines1 = string.Join("\n", ["x", "y", "z"]);
        var lines2 = string.Join("\n", ["1", "2", "3", "4", "5"]);

        var list1 = await this.Create(new StringReader(lines1), new StringReader(lines2))
            .ToListAsync();
        string.Join(", ", list1).ShouldEqual("x, 1, y, 2, z, 3, 4, 5");

        var list2 = await this.Create(new StringReader(lines2), new StringReader(lines1))
            .ToListAsync();
        string.Join(", ", list2).ShouldEqual("1, x, 2, y, 3, z, 4, 5");
    }

    [Test]
    public void TestConsumeTwice()
    {
        var asyncEnumerable = this.Create(new StringReader("a"), new StringReader("b"));
        asyncEnumerable.GetAsyncEnumerator();
        Assert.Throws<InvalidOperationException>(() => asyncEnumerable.GetAsyncEnumerator());
    }

    [Test]
    public void TestOneThrows()
    {
        void TestOneThrows(bool reverse)
        {
            var reader1 = new StringReader("a\nb\nc");
            var count = 0;
            var mockReader = new Mock<TextReader>(MockBehavior.Strict);
            mockReader.Setup(r => r.ReadLineAsync())
                .ReturnsAsync(() => ++count < 3 ? "LINE" : throw new TimeZoneNotFoundException());

            Assert.ThrowsAsync<TimeZoneNotFoundException>(
                async () => await this.Create(
                        reverse ? mockReader.Object : reader1,
                        reverse ? reader1 : mockReader.Object
                    ).ToListAsync()
            );
        }

        TestOneThrows(reverse: false);
        TestOneThrows(reverse: true);
    }

    [Test, Timeout(10_000)] // something's wrong if it's taking more than 10 seconds
    public async Task FuzzTest()
    {
        Pipe pipe1 = new(), pipe2 = new();

        var asyncEnumerable = this.Create(new StreamReader(pipe1.OutputStream), new StreamReader(pipe2.OutputStream));

        var strings1 = Enumerable.Range(0, 2000).Select(_ => Guid.NewGuid().ToString()).ToArray();
        var strings2 = Enumerable.Range(0, 2300).Select(_ => Guid.NewGuid().ToString()).ToArray();

        static void WriteStrings(IReadOnlyList<string> strings, Pipe pipe)
        {
            SpinWait spinWait = default;
            Random random = new(Guid.NewGuid().GetHashCode());
            using StreamWriter writer = new(pipe.InputStream);
            foreach (var line in strings)
            {
                if (random.Next(10) == 1)
                {
                    spinWait.SpinOnce();
                }

                writer.WriteLine(line);
            }
        }

        var task1 = Task.Run(() => WriteStrings(strings1, pipe1));
        var task2 = Task.Run(() => WriteStrings(strings2, pipe2));
        Task.WaitAll(task1, task2); // need to dispose the writer to end the stream

        CollectionAssert.AreEquivalent(strings1.Concat(strings2), await asyncEnumerable.ToListAsync());
    }
}

public static class AsyncEnumerableExtensions
{
    public static async Task<List<string>> ToListAsync(this IAsyncEnumerable<string> strings)
    {
        List<string> result = [];
        await foreach (var item in strings) { result.Add(item); }
        return result;
    }
}
