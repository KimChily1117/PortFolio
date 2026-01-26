// Fill out your copyright notice in the Description page of Project Settings.


#include "LyraCloneSystemStatics.h"


void ULyraCloneSystemStatics::SetScalarParameterValueOnAllMeshComponents(AActor* TargetActor, const FName ParameterName, const float ParameterValue, bool bIncludeChildActors)
{
	if (TargetActor != nullptr)
	{
		TargetActor->ForEachComponent<UMeshComponent>(bIncludeChildActors, [=](UMeshComponent* InComponent)
			{
				InComponent->SetScalarParameterValueOnMaterials(ParameterName, ParameterValue);
			});
	}
}

void ULyraCloneSystemStatics::SetVectorParameterValueOnAllMeshComponents(AActor* TargetActor, const FName ParameterName, const FVector ParameterValue, bool bIncludeChildActors)
{
	if (TargetActor != nullptr)
	{
		TargetActor->ForEachComponent<UMeshComponent>(bIncludeChildActors, [=](UMeshComponent* InComponent)
			{
				InComponent->SetVectorParameterValueOnMaterials(ParameterName, ParameterValue);
			});
	}
}

void ULyraCloneSystemStatics::SetColorParameterValueOnAllMeshComponents(AActor* TargetActor, const FName ParameterName, const FLinearColor ParameterValue, bool bIncludeChildActors)
{
	SetVectorParameterValueOnAllMeshComponents(TargetActor, ParameterName, FVector(ParameterValue), bIncludeChildActors);

}

TArray<UActorComponent*> ULyraCloneSystemStatics::FindComponentsByClass(AActor* TargetActor, TSubclassOf<UActorComponent> ComponentClass, bool bIncludeChildActors)
{
	TArray<UActorComponent*> Components;
	if (TargetActor != nullptr)
	{
		TargetActor->GetComponents(ComponentClass, /*out*/ Components, bIncludeChildActors);

	}
	return MoveTemp(Components);
}
