using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;

namespace ColumnSubsets
{
    class HierarchyBuilder
    {
        /// <summary>
        /// Dynamic creation of hierarchical classes with distinct column subsets.
        /// 
        /// Let's say we have the following column sets:
        /// { "Id", "DateCreated", "DateDeleted" }, { "Id", "DateCreated", "DateDeleted", "Name" }, { "Id", "CustomerId", "DateCreated", "Name" }.
        /// 
        /// Assumptions: 
        /// - all column names are case-sensitive;
        /// - all column types are typeof(string).
        ///  
        /// 1. Distinct column subsets that are present in 2+ column sets are: 
        /// 
        /// { "Id", "DateCreated" }, { "Id", "Name" }, { "Id", "DateCreated", "DeteDeleted" }, { "Id", "DateCreated", Name" }.
        ///  
        /// 2. Hierarchy of column subsets is:
        /// 
        /// { "Id", "DateCreated" }, 
        /// { "Id", "Name" }, 
        /// { "Id", "DateCreated", "DeteDeleted" } -> { "Id", "DateCreated" }, 
        /// { "Id", "DateCreated", Name" } -> { "Id", "DateCreated" } (or it could be { "Id", "DateCreated", Name" } -> { "Id", "Name" })
        /// 
        /// 3. Hierarchical class structure is:
        /// 
        /// public class ColumnSubset1 : IColumnSubset {
        ///     public string Id;
        ///     public string DateCreated;
        /// }
        /// 
        /// public class ColumnSubset2 : IColumnSubset {
        ///     public string Id;
        ///     public string Name;
        /// }
        /// 
        /// public class ColumnSubset3 : ColumnSubset1 {
        ///     public string DateDeleted;
        /// }
        /// 
        /// public class ColumnSubset4 : ColumnSubset1 {
        ///     public string Name;
        /// }
        /// 
        /// </summary>
        public void CreateClassHiererchy(List<List<string>> columns)
        {
            // *** Phase 1: Build a hierarchical ColumnSubsetInfo structure

            // step 1: find all distinct column combinations(subsets) that belong to 2 or more input column sets
            // Assumption: all column names are case-sensitive (oterwise, we'll add the line to convert them to lower or upper case)
            var distinctColumnCombinations = FindAllDistinctCombinations(columns, 2);
            Console.WriteLine("\nDistinct subsets of recurring column names:\n-------------------------------------------");
            distinctColumnCombinations.ToList().ForEach(c => Console.WriteLine($"({String.Join(",", c)})"));

            var columnSubsetInfos = new List<IColumnSubsetInfo<string>>(); // distinct subsets of recurrent column names

            distinctColumnCombinations.ToList().ForEach(c => columnSubsetInfos.Add(new ColumnSubsetInfo<string>(c)));
            // at this point columnSubsets - independent ColumnSubsetInfo objects, ordered by column count

            // step 2: build hirerchy:
            // The idea:
            // 1. reverse the collection, so it starts with the longest subset
            // 2. for each subset find the closest subset, all columns of which are present in the current subset. 
            // If we find one - it is the parent (base) subset. Base subset will have the most number of columns present in the derived class
            columnSubsetInfos.Reverse();
            for (var i = 0; i < columnSubsetInfos.Count - 1; i++)
            {
                var columnSet = columnSubsetInfos[i];
                var parentColumnSet = columnSubsetInfos.Skip(i + 1).FirstOrDefault(c => !c.GetColumns().Except(columnSet.GetColumns()).Any());
                if (parentColumnSet != null)
                    columnSet.SetParentSubset(parentColumnSet);
            }

            columnSubsetInfos.Reverse(); // at this point columnSubsetInfos - hierarchical ColumnSubsetInfo objects

            Console.WriteLine("\nDistinct subset hierarchy:\n-------------------------------------------");
            columnSubsetInfos.ForEach(c => Console.WriteLine(c));

            // *** Phase 2: Create hierarchical classes based on hierarchical columnSubsetInfo structure
            CreateSubsetClasses<string>(columnSubsetInfos);
        }

        private IEnumerable<IEnumerable<T>> FindAllDistinctCombinations<T>(IEnumerable<IEnumerable<T>> inputCollections, int minCombinationSize = 0)
        {
            var allColumnCombinations = new List<IEnumerable<T>>();
            foreach (var collection in inputCollections)
                allColumnCombinations.AddRange(FindAllDistinctCombinationsInCollection(collection, minCombinationSize));

            var duplicateItems = allColumnCombinations.GroupBy(x => x).Where(x => x.Count() > 1).Select(x => x.Key);

            // grab only column subsets that belong to 2 or more column subsets (in other words - not distinct)
            var distinctColumnCombinations = allColumnCombinations.Except(allColumnCombinations.Distinct(new CollectionComparer<T>()));

            // make sure the values are unique and order them
            distinctColumnCombinations = distinctColumnCombinations.Distinct(new CollectionComparer<T>()).OrderBy(c => c.Count());
            return distinctColumnCombinations;
        }

        private IEnumerable<IEnumerable<T>> FindAllDistinctCombinationsInCollection<T>(IEnumerable<T> source, int minCombinationSize) =>
            Enumerable.Range(0, 1 << source.Count())
                      .Select(index => source.Where((v, i) => (index & (1 << i)) != 0)) // find all possible combinations
                      .Where(x => x.Count() >= minCombinationSize);                     // return only combinations with minCombinationSize+ elements

        // create hierarchical Subset classes based on hierarchical ColumnSubsetInfo objects
        private void CreateSubsetClasses<T>(IEnumerable<IColumnSubsetInfo<T>> columnSubsetInfos)
        {
            var assemblyName = new AssemblyName("Funcular.ColumnSubsets");
            var assemblyBuilder = AppDomain.CurrentDomain.DefineDynamicAssembly(assemblyName, AssemblyBuilderAccess.RunAndSave);
            var moduleBuilder = assemblyBuilder.DefineDynamicModule(assemblyName.Name);

            var classDictionary = new Dictionary<int, Type>();
            foreach (var s in columnSubsetInfos)
            {
                TypeBuilder typeBuilder;
                if (s.GetParentSubset() == null)
                {
                    // create base (parentless) class
                    typeBuilder = moduleBuilder.DefineType($"ColumnSubset{s.GetId()}", TypeAttributes.Public);
                    typeBuilder.AddInterfaceImplementation(typeof(IColumnSubset));
                }
                else
                    // create derived  class
                    typeBuilder = moduleBuilder.DefineType($"ColumnSubset{s.GetId()}", TypeAttributes.Public, classDictionary[s.GetParentSubset().GetId()]);

                // Add public class variable for each column. Assumption - all variables are typeof (string)
                // If needed, we can create class properties instead of public fields
                s.GetColumns().ToList().ForEach(c => typeBuilder.DefineField(c.ToString(), typeof(string), FieldAttributes.Public));

                var subsetType = typeBuilder.CreateType();
                classDictionary[s.GetId()] = subsetType;
            };
            assemblyBuilder.Save($"{assemblyName.Name}.dll");
        }
    }

    interface IColumnSubsetInfo<T>
    {
        IEnumerable<T> GetColumns();

        int GetId();

        void SetParentSubset(IColumnSubsetInfo<T> parent);

        IColumnSubsetInfo<T> GetParentSubset();
    }

    class ColumnSubsetInfo<T> : IColumnSubsetInfo<T>
    {
        private static int IdCounter;

        private readonly int _id; // for the sake of simplicity: just incremental int ids
        private IColumnSubsetInfo<T> _parent;
        private IEnumerable<T> _columns = new List<T>();

        public ColumnSubsetInfo(IEnumerable<T> columns)
        {
            if (columns == null)
                throw new ArgumentNullException("columns");
            _id = ++IdCounter; // simple id generator
            _columns = columns;
        }

        public int GetId() => _id;

        public IColumnSubsetInfo<T> GetParentSubset() => _parent;

        public void SetParentSubset(IColumnSubsetInfo<T> parent)
        {
            if (GetParentSubset() != null)
                throw new Exception($"Subset {this} already has a parent subset.");
            _parent = parent;
            // when assigning a parent, remove all columns inherited from this parent, so a 'derived' object contains only its own columns  
            _columns = _columns.Except(parent.GetColumns()).ToList();
        }

        public IEnumerable<T> GetColumns() => _columns; // return just its own columns

        // return all columns of a column subset (inherited + its own)
        // just for test purposes
        public IEnumerable<T> GetColumnsIncludeInherited()
        {
            var allColumns = _parent?.GetColumns() as List<T> ?? new List<T>();
            foreach (var column in _columns)
                allColumns.Add(column);
            return allColumns;
        }

        public override string ToString()
        {
            //return $"ID={GetId()}, Parent={(_parent!=null ? String.Join(",", _parent?.GetColumns()) : null)}, Columns={String.Join(",", _columns)}";

            //let's output subsets with their hierarcy
            var str = $"({String.Join(",", GetColumns())})";
            for (IColumnSubsetInfo<T> subset = this; subset.GetParentSubset() != null; subset = subset.GetParentSubset())
                str += $" -> ({String.Join(",", subset.GetParentSubset().GetColumns())})";
            return str;
        }
    }

    // collection comparer    
    class CollectionComparer<T> : IEqualityComparer<IEnumerable<T>>
    {
        public bool Equals(IEnumerable<T> x, IEnumerable<T> y) => x.SequenceEqual(y);

        public int GetHashCode(IEnumerable<T> obj)
        {
            int hashCode = 0;
            for (var index = 0; index < obj.Count(); index++)
                hashCode ^= new { Index = index, Item = obj.ElementAt(index) }.GetHashCode();
            return hashCode;
        }
    }
}
