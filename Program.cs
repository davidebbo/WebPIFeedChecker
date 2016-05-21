using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;

namespace WebPIFeedChecker
{
    class Program
    {
        static Dictionary<string, FeedEntry> _entries;

        static void Main(string[] args)
        {
            if (args.Length < 1 || args.Length > 2)
            {
                Console.WriteLine("Syntax: WebPIFeedChecker.exe FeedFile [PackageFolder]");
                return;
            }

            string feedFile = args[0];
            string packageFolder = args.Length > 1 ? args[1] : null;

            var reader = new StreamReader(feedFile, true);

            var root = XElement.Load(reader);
            XNamespace ns = "http://www.w3.org/2005/Atom";


            var entries = from entry in root.Descendants(ns + "entry")
                          select new FeedEntry
                          {
                              Id = entry.Elements(ns + "productId").First().Value,
                              Reachable = false,
                              Dependencies = from dependency in entry.Descendants(ns + "dependency")
                                             select dependency.Descendants(ns + "productId").First().Value
                          };
            _entries = entries.ToDictionary(e => e.Id);


            // First entry is the root node
            WalkFeedEntry(_entries.Values.First());

            foreach (var entry in _entries.Values.Where(e => !e.Reachable))
            {
                Console.WriteLine($"{entry.Id}");
            }
        }

        static void WalkFeedEntry(FeedEntry entry)
        {
            entry.Reachable = true;

            foreach (var dependencyId in entry.Dependencies)
            {
                WalkFeedEntry(_entries[dependencyId]);
            }
        }

        class FeedEntry
        {
            public string Id { get; set; }
            public bool Reachable { get; set; }
            public IEnumerable<string> Dependencies { get; set; }
        }
    }
}
