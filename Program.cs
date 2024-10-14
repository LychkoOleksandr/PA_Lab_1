using System.Diagnostics;

namespace Lab_1_PA
{
    class Program
    {
        static async Task Main(string[] args)
        {
            int fileSize = 1024;

            if (args.Length > 0)
            {
                if (int.TryParse(args[0], out int result))
                {
                    fileSize = result;
                    Console.WriteLine($"Received file size: {fileSize} MB");
                }
                else
                {
                    Console.WriteLine("Invalid integer argument. Using default value of 1GB.");
                }
            }
            else
            {
                Console.WriteLine("No argument provided. Using default value of 1GB.");
            }
            
            long memoryLimitInBytes = GetAvailableMemoryFromCGroup();

            if (memoryLimitInBytes == 0)
            {
                Console.WriteLine("Cgroup memory limit is not set. Using a default of 512MB.");
                memoryLimitInBytes = 512 * 1024 * 1024;
            }

            long safeMemoryLimitInBytes = (long)(memoryLimitInBytes * 0.75);
            long averageIntegerSizeInBytes = 10 + Environment.NewLine.Length;
            
            int blockSize = (int)(safeMemoryLimitInBytes / averageIntegerSizeInBytes);

            Console.WriteLine($"Memory limit: {memoryLimitInBytes / (1024 * 1024)} MB");
            Console.WriteLine($"Safe memory limit: {safeMemoryLimitInBytes / (1024 * 1024)} MB");

            string filePath = Path.Combine(Directory.GetCurrentDirectory(), "random_numbers.txt");
            string sortedFilePath = Path.Combine(Directory.GetCurrentDirectory(), "sorted_numbers.txt");

            long fileSizeInBytes = fileSize * 1024 * 1024;
            CreateRandomNumbersFile(filePath, fileSizeInBytes);

            Stopwatch timer = Stopwatch.StartNew();

            List<string> tempFiles = await SortChunksWithMergeSort(filePath, blockSize);

            Console.WriteLine($"Number of chunks created: {tempFiles.Count}");

            MergeSortedChunks(tempFiles, sortedFilePath);

            timer.Stop();
            Console.WriteLine($"Time elapsed: {timer.Elapsed}");

            Console.WriteLine("Sorting finished");
            Console.ReadLine();
        }

        static long GetAvailableMemoryFromCGroup()
        {
            string filePath = "/sys/fs/cgroup/memory/memory.limit_in_bytes";
            if (File.Exists(filePath))
            {
                string limit = File.ReadAllText(filePath);
                return long.Parse(limit);
            }
            else
            {
                Console.WriteLine("Cgroup memory limit file not found.");
                return 0;
            }
        }

        static void CreateRandomNumbersFile(string filePath, long fileSizeInBytes)
        {
            using (StreamWriter writer = new StreamWriter(filePath))
            {
                Random random = new Random();
                long averageIntegerSizeInBytes = 10 + Environment.NewLine.Length;
                long numberOfIntegers = fileSizeInBytes / averageIntegerSizeInBytes;

                for (long i = 0; i < numberOfIntegers; i++)
                {
                    int randomNumber = random.Next(int.MinValue, int.MaxValue);
                    writer.WriteLine(randomNumber);
                }
            }

            Console.WriteLine($"File {filePath} created successfully");
        }

        static Task<List<string>> SortChunksWithMergeSort(string filePath, int blockSize)
        {
            List<string> tempFiles = new List<string>();

            using StreamReader reader = new StreamReader(filePath);
            int[] numbers = new int[blockSize];
            int[] buffer = new int[blockSize]; 

            while (!reader.EndOfStream)
            {
                int currentIndex = 0;

                while (currentIndex < blockSize && !reader.EndOfStream)
                {
                    string line = reader.ReadLine();
                    if (int.TryParse(line, out int number))
                    {
                        numbers[currentIndex++] = number;
                    }
                }
                
                MergeSort(numbers, buffer, 0, currentIndex - 1); 
                
                string tempFilePath = Path.GetTempFileName();
                tempFiles.Add(tempFilePath);

                using StreamWriter writer = new StreamWriter(tempFilePath);
                
                for (int i = 0; i < currentIndex; i++)
                {
                    writer.WriteLine(numbers[i]);
                }
            }

            return Task.FromResult(tempFiles);
        }
        
        static void MergeSort(int[] array, int[] buffer, int left, int right)
        {
            if (left < right)
            {
                int middle = (left + right) / 2;

                MergeSort(array, buffer, left, middle);
                MergeSort(array, buffer, middle + 1, right);

                Merge(array, buffer, left, middle, right);
            }
        }

        static void Merge(int[] array, int[] buffer, int left, int middle, int right)
        {
            int i = left, j = middle + 1, k = left;
            
            for (int l = left; l <= right; l++)
            {
                buffer[l] = array[l]; 
            }
            
            while (i <= middle && j <= right)
            {
                if (buffer[i] <= buffer[j])
                {
                    array[k] = buffer[i];
                    i++;
                }
                else
                {
                    array[k] = buffer[j];
                    j++;
                }
                k++;
            }

            while (i <= middle)
            {
                array[k] = buffer[i];
                i++;
                k++;
            }
        }

        static void MergeSortedChunks(List<string> tempFiles, string sortedFilePath)
        {
            using (StreamWriter sortedWriter = new StreamWriter(sortedFilePath))
            {
                var readers = tempFiles.Select(file => new StreamReader(file)).ToList();
                var minHeap = new SortedDictionary<int, Queue<int>>();

                for (int i = 0; i < readers.Count; i++)
                {
                    if (int.TryParse(readers[i].ReadLine(), out int value))
                    {
                        if (!minHeap.ContainsKey(value))
                        {
                            minHeap[value] = new Queue<int>();
                        }

                        minHeap[value].Enqueue(i);
                    }
                }

                while (minHeap.Count > 0)
                {
                    var minEntry = minHeap.First();
                    int minValue = minEntry.Key;
                    int chunkIndex = minEntry.Value.Dequeue();

                    if (minEntry.Value.Count == 0)
                    {
                        minHeap.Remove(minValue);
                    }

                    sortedWriter.WriteLine(minValue);

                    if (int.TryParse(readers[chunkIndex].ReadLine(), out int nextValue))
                    {
                        if (!minHeap.ContainsKey(nextValue))
                        {
                            minHeap[nextValue] = new Queue<int>();
                        }

                        minHeap[nextValue].Enqueue(chunkIndex);
                    }
                }

                foreach (var reader in readers)
                {
                    reader.Close(); 
                }
            }

            foreach (var tempFile in tempFiles)
            {
                File.Delete(tempFile); 
            }
        }
    }
}
