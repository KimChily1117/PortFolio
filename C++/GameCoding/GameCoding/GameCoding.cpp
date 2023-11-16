#include <iostream>
using namespace std;
#include <queue>


void Test()
{
	cout << "Hello World Func" << endl;


}

int AddTest(int a, int b)
{
	return a + b;
}





int main()
{
	//함수 포인터 써보기
	// 
	// 
	
	using FuncPtrType = void(*)();
	FuncPtrType func = Test;
	
	func();

	using FuncAddTest = int(*)(int, int);
	FuncAddTest addfunc = AddTest;


	int ret = addfunc(1, 2);




}