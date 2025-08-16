// Fill out your copyright notice in the Description page of Project Settings.


#include "LyraCloneCameraMode_ThirdPerson.h"
#include "Curves/CurveVector.h"

#include UE_INLINE_GENERATED_CPP_BY_NAME(LyraCloneCameraMode_ThirdPerson)


ULyraCloneCameraMode_ThirdPerson::ULyraCloneCameraMode_ThirdPerson(const FObjectInitializer& ObjectInitializer) : Super(ObjectInitializer)
{
}

void ULyraCloneCameraMode_ThirdPerson::UpdateView(float DeltaTime)
{
	FVector PivotLocation = GetPivotLocation();
	FRotator PivotRotation = GetPivotRotation();

	PivotRotation.Pitch = FMath::ClampAngle(PivotRotation.Pitch, ViewPitchMin, ViewPitchMax);

	View.Location = PivotLocation;
	View.Rotation = PivotRotation;
	View.ControlRotation = View.Rotation;
	View.FieldOfView = FieldOfView;

	// TargetOffsetCurve�� �������̵�Ǿ� �ִٸ�, Curve�� ���� �����ͼ� ���� ����
	// - Camera �������� Charater�� ��� �κп� Target���� ���� �����ϴ� ������ �����ϸ� ��
	if (TargetOffsetCurve)
	{
		const FVector TargetOffset = TargetOffsetCurve->GetVectorValue(PivotRotation.Pitch);
		View.Location = PivotLocation + PivotRotation.RotateVector(TargetOffset);
	}

	UE_LOG(LogTemp, Warning, TEXT("PivotRotation: Pitch=%f, Yaw=%f, Roll=%f"),
		PivotRotation.Pitch, PivotRotation.Yaw, PivotRotation.Roll);

}
