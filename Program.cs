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
                              Id = entry.Element(ns + "productId").Value,
                              Dependencies = from dependency in entry.Descendants(ns + "dependency")
                                             select dependency.Descendants(ns + "productId").First().Value
                          };
            _entries = entries.ToDictionary(e => e.Id);


            // First entry is the root node
            WalkFeedEntry(_entries.Values.First());

            foreach (var element in root.Element(ns + "featuredProducts").Elements(ns + "productId"))
            {
                FeedEntry entry;
                if (_entries.TryGetValue(element.Value, out entry))
                {
                    WalkFeedEntry(entry);
                }
                else
                {
                    Console.WriteLine("Missing featured product: " + element.Value);
                }
            }

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
                FeedEntry depEntry;
                if (_entries.TryGetValue(dependencyId, out depEntry))
                {
                    WalkFeedEntry(depEntry);
                }
                else
                {
                    Console.WriteLine("Missing dependency: " + dependencyId);
                }
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
