#include <iostream>
using namespace std;

class Pos
{
public :
	int x = 0;
	int y = 0;



	Pos operator+()
};



int main()
{
	
	Pos *A = new Pos();
	Pos *B = new Pos();


	Pos C = A + B;
}