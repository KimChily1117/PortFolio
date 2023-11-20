using System;
using System.Collections.Generic;
using System.Text;

namespace Algorithm
{
    public class Solve
    {
        public void Addic()
        {

            // 입력부 
            string arrInput = Console.ReadLine();
            string[] arr = arrInput.Split(' ');

            int N = Convert.ToInt32(arr[0]);
            int M = Convert.ToInt32(arr[1]);    
            

            string elementInput = Console.ReadLine();
            string[] tmp = elementInput.Split(' ');

            int[] eleArr = Array.ConvertAll(tmp, s => int.Parse(s));


            int[] sumArr = new int[eleArr.Length];


            sumArr[0] = eleArr[0];
            for (int i = 1; i < sumArr.Length; i++)
            {              
                sumArr[i] = sumArr[i-1] + eleArr[i];
            }

            // 연산부


            for (int k = 0; k < M; k++)
            {
                string ij = Console.ReadLine();
                string[] ijArr = ij.Split(' ');
                int I = Convert.ToInt32(ijArr[0]);
                int J = Convert.ToInt32(ijArr[1]);


                int result = sumArr[J] - sumArr[I - 1];

                Console.WriteLine(result);
            }



        }




    }
}
