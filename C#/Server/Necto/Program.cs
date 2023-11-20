namespace Necto
{


    



    class Result
    {





        static int[] GetFailureFunction(string pattern)
        {
            int m = pattern.Length;
            int[] f = new int[m];
            int j = 0;

            for (int i = 1; i < m; i++)
            {
                while (j > 0 && pattern[i] != pattern[j])
                {
                    j = f[j - 1];
                }

                if (pattern[i] == pattern[j])
                {
                    f[i] = ++j;
                }
            }

            return f;
        }





        public static List<int> autocompletes(List<string> inputs) 
        {
            // i - 1 번째까지 기록으로 본다. 
           

            List<int> returnvalue = new List<int>();


            for (int i = 0; i < inputs.Count; i++)
            {
                int[] f = GetFailureFunction(inputs[i]);

            }



            return returnvalue;
        
        }
    }

    class Graph
    {
        List<int>[] adj = new List<int>[]
        {
            new List<int>() { 1, 2 },
            new List<int>() { 0 },
            new List<int>() { 1,}
        };



        //public static int minimumMovement(List<int> obstacleLanes)
        //{



        //}


    }




    internal class Program
    {
        public static void FindTriplets(int[] arr, int targetSum)
        {
            int n = arr.Length;

            if (n < 3)
            {
                Console.WriteLine("배열에는 적어도 3개의 원소가 필요합니다.");
                return;
            }

            for (int i = 0; i < n - 2; i++)
            {
                for (int j = i + 1; j < n - 1; j++)
                {
                    for (int k = j + 1; k < n; k++)
                    {
                        int currentSum = arr[i] + arr[j] + arr[k];
                        if (currentSum <= targetSum)
                        {
                            Console.WriteLine($"Triplets: {arr[i]}, {arr[j]}, {arr[k]}");
                        }
                    }
                }
            }
        }

        public static void Main()
        {
            int[] arr = { 3,1,2,4 };
            int targetSum = 7;

            FindTriplets(arr, targetSum);
        
    






    //int inputsCount = Convert.ToInt32(Console.ReadLine().Trim());

    //List<string> inputs = new List<string>();

    // for (int i = 0; i < inputsCount; i++)
    // {
    //     string inputsItem = Console.ReadLine();
    //     inputs.Add(inputsItem);
    // }

    // List<int> result = Result.autocompletes(inputs);


}
    }
}