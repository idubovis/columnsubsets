using System;
using System.Collections.Generic;

namespace ColumnSubsets
{
    /// <summary>
    /// Interface that all ColumnSubset classes must implement.
    /// </summary>
    public interface IColumnSubset
    {
        // For now, it is just a marker interface 
    }

    class Program
    {
        static void Main(string[] args)
        {
            var columns = new List<List<string>>() {
                new List<string> { "Id", "DateCreated", "DateDeleted" },
                new List<string> { "Id", "DateCreated", "Name" },
                new List<string> { "Id", "DateCreated", "DateDeleted", "Name", "Gender" },
                new List<string> { "Id", "DateCreated", "DateDeleted", "Name", "State"},
                new List<string> { "Id", "Position", "Gender" },
                new List<string> { "Id", "OrderID", "OrderDate" },
                new List<string> { "Id", "Name" }
            };

            Console.WriteLine("Input column sets:\n-------------------------------------------");
            columns.ForEach(c => Console.WriteLine(String.Join(",", c)));

            // * build hierarchy from a list of column sets
            //new HierarchyBuilder().CreateClassHiererchy(columns);

            // * build hierarchy from a list of column sets, and a set of potential base classes
            new HierarchyBuilder2().CreateClassHiererchy(columns, "EntityClasses", typeof(EntityClasses.IColumnSubset));

            Console.WriteLine("\nPress any key to close...");
            Console.ReadKey();
        }
    }
}
