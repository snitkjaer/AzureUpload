using System;

using System.Threading;
using System.Threading.Tasks;

using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;

using System.IO;
using System.IO.Compression;

using System.ComponentModel;


namespace AzureUpload.Runner
{
    public static class ExtensionMethods
    {
        //
        // Async
        //
		/*
		public static void ForEach<T>(this IEnumerable<T> source, Action<T> body)
		{
			foreach (var item in source) body(item);
		}
		*/

		public static Task ForEachAsync<T>(this IEnumerable<T> source, Func<T, Task> body)
		{
            int dop = 16;

			return Task.WhenAll(
				from partition in Partitioner.Create(source).GetPartitions(dop)
				select Task.Run(async delegate
				{
					using (partition)
						while (partition.MoveNext())
							await body(partition.Current);
				}));
		}


        public static Task ForEachAsync<T>(this IEnumerable<T> source, int dop, Func<T, Task> body)
		{
			return Task.WhenAll(
				from partition in Partitioner.Create(source).GetPartitions(dop)
				select Task.Run(async delegate
				{
					using (partition)
						while (partition.MoveNext())
							await body(partition.Current);
				}));
		}

        //
        // Zip
        //

		public static bool ValidateZip(this string fileName)
		{
			return ValidateZip(new FileInfo(fileName));
		}

		public static bool ValidateZip(this FileInfo file)
		{
			bool result = false;
			if (file.Exists)
			{
				try
				{
					using (var archive = ZipFile.OpenRead(file.FullName))
						result = archive.Entries.Count > 0;
				}
				catch (Exception)
				{
					result = false;
				}
			}
			return result;
		}




		public static T Get<T>(this string value)
		{
			

			var converter = TypeDescriptor.GetConverter(typeof(T));
			return (T)(converter.ConvertFromInvariantString(value));
		}
    }


}
