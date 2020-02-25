using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;

namespace ColumnSubsets
{
    class HierarchyBuilder2
    {
        /// <summary>
        /// For the given column sets =>
        /// find base classes from the given assembly and create concrete classes
        /// </summary>
        /// <param name="columns"></param>
        /// <param name="entityAssemblyName"></param>
        /// <param name="implementsInterface"></param>
        public void CreateClassHiererchy(List<List<string>> columns, string entityAssemblyName, Type implementsInterface = null)
        {
            Console.WriteLine("\nExisting Entity Classes:\n-------------------------------------------");
            Assembly.Load(entityAssemblyName).GetTypes().ToList().ForEach(c => Console.WriteLine(TypeToString(c)));

            var assemblyName = new AssemblyName("Funcular.ColumnSubsets");
            var assemblyBuilder = AppDomain.CurrentDomain.DefineDynamicAssembly(assemblyName, AssemblyBuilderAccess.RunAndSave);
            var moduleBuilder = assemblyBuilder.DefineDynamicModule(assemblyName.Name);

            var newClassID = 10; // for simplicity
            Console.WriteLine("\nCreated classes:\n-------------------------------------------");
            foreach (var columnSet in columns)
            {
                var baseType = GetClosestBaseClass(columnSet, entityAssemblyName, implementsInterface);
                //Console.WriteLine($"[{String.Join(", ", columnSet)}] -> {TypeToString(baseType)}");

                TypeBuilder typeBuilder;
                if (baseType.IsInterface)
                {
                    // create base (parentless) class
                    typeBuilder = moduleBuilder.DefineType($"ColumnSubset{newClassID++}", TypeAttributes.Public);
                    typeBuilder.AddInterfaceImplementation(baseType);
                    // Add public class variable for each column
                    columnSet.ToList().ForEach(c => typeBuilder.DefineField(c.ToString(), typeof(string), FieldAttributes.Public));
                }
                else
                {
                    // create derived class
                    typeBuilder = moduleBuilder.DefineType($"ColumnSubset{newClassID++}", TypeAttributes.Public, baseType);
                    // Add public class variable for each column that is not inherited from the base class
                    var baseClassPublicFields = baseType.GetFields(GetBindingFlags(true)).Select(f => f.Name);
                    // ignore fields that are inherited from the base class
                    var filteredColumnSet = columnSet.Except(baseClassPublicFields);
                    filteredColumnSet.ToList().ForEach(c => typeBuilder.DefineField(c.ToString(), typeof(string), FieldAttributes.Public));
                }
                var type = typeBuilder.CreateType();

                Console.WriteLine($"Column Subset: [{String.Join(", ", columnSet)}] =>\n\tClass: {TypeToString(type, false)} -> {TypeToString(type.BaseType)}");
            }

            assemblyBuilder.Save($"{assemblyName.Name}.dll");
        }

        /// <summary>
        /// Find the closest base class for a given columnSet from all classes in assemblyName that implement the given interface (optional).
        /// The method looks up for a the closest class. If it can't find any, it returns the provided base interface 
        /// 
        /// </summary>
        /// <param name="columnSet"></param>
        /// <param name="assemblyName"></param>
        /// <param name="implementsInterface"></param>
        /// <returns></returns>
        private Type GetClosestBaseClass(IEnumerable<string> columnSet, string assemblyName, Type implementsInterface = null)
        {
            var potentialBaseClasses = new List<Type>();
            foreach (var type in Assembly.Load(assemblyName).GetTypes())
            {
                if (type.IsClass && (implementsInterface == null || implementsInterface.IsAssignableFrom(type)))
                    potentialBaseClasses.Add(type);
            }
            potentialBaseClasses = potentialBaseClasses.OrderByDescending(x => x.GetFields(GetBindingFlags(true)).Count()).ToList(); 

            foreach (var type in potentialBaseClasses)
            {
                // take into account not just declared fields, but inherited as well
                var classPublicFields = type.GetFields(GetBindingFlags(true)).Select(f => f.Name);
                if (!classPublicFields.Except(columnSet).Any())
                    return type;
            }

            return implementsInterface;
        }

        private string TypeToString(Type type, bool includeInheritedFields = true) =>
            $"{type}({String.Join(", ", type.GetFields(GetBindingFlags(includeInheritedFields)).ToList())})";

        private BindingFlags GetBindingFlags(bool includeInheritedFields)
        {
            var flags = BindingFlags.Public | BindingFlags.Instance;
            if (!includeInheritedFields) flags |= BindingFlags.DeclaredOnly; 
            return flags;
        }
    }
}
