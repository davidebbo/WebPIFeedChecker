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
            if (args.Length < 1)
            {
                Console.WriteLine("Syntax: WebPIFeedChecker.exe roots feed [feed...]");
                return;
            }

            var entries = args.Skip(1).SelectMany(ReadFeed);
            _entries = entries.ToDictionary(e => e.Id, StringComparer.OrdinalIgnoreCase);

            string[] roots = File.ReadAllLines(args[0]);
            foreach (string root in roots)
            {
                FeedEntry entry;
                if (!_entries.TryGetValue(root, out entry))
                {
                    throw new ArgumentException($"Can't find root {roots}");
                }

                WalkFeedEntry(entry);
            }

            Console.WriteLine("Unused products:");
            foreach (var entry in _entries.Values.Where(e => !e.Reachable))
            {
                Console.WriteLine($"{entry.Id}");
            }
        }

        static IEnumerable<FeedEntry> ReadFeed(string feedFile)
        {
            var reader = new StreamReader(feedFile, detectEncodingFromByteOrderMarks: true);

            var root = XElement.Load(reader);
            XNamespace ns = "http://www.w3.org/2005/Atom";

            XElement dependenciesNode = root.Element(ns + "dependencies") ?? new XElement("dummy");

            var topDeps = from dependency in dependenciesNode.Elements(ns + "dependency")
                          select new
                          {
                              Id = dependency.Attribute("id").Value,
                              Dependencies = from productId in dependency.Descendants(ns + "productId")
                                             select productId.Value
                          };
            var topDepsDict = topDeps.ToDictionary(e => e.Id, StringComparer.OrdinalIgnoreCase);

            Func<XElement, IEnumerable<string>> productsIds = (dependency) =>
            {
                var productId = dependency.Descendants(ns + "productId").FirstOrDefault();
                if (productId == null)
                {
                    string idref = dependency.Attribute("idref").Value;
                    return topDepsDict[idref].Dependencies;
                }

                return Enumerable.Repeat(productId.Value, 1);
            };

            return from entry in root.Descendants(ns + "entry")
                   select new FeedEntry
                   {
                       Id = entry.Element(ns + "productId").Value,
                       Dependencies = entry.Descendants(ns + "dependency").SelectMany(productsIds)
                                        .Concat(entry.Descendants(ns + "updates").SelectMany(productsIds))
                   };
        }

        static void WalkFeedEntry(FeedEntry entry)
        {
            // No need to process again
            if (entry.Reachable) return;

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

            public override string ToString() { return Id; }
        }
    }
}
