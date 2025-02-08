﻿#pragma once

struct FCSAssembly;

enum ETypeState : uint8
{
	// The type is up to date. No need to rebuild or update.
	UpToDate,

	// The type needs to be rebuilt. The structure has changed.
	NeedRebuild,

	// The type just needs to be updated. New method ptr et.c.
	NeedUpdate,
};

template<typename TMetaData, typename TField, typename TTypeBuilder>
struct UNREALSHARPCORE_API TCSharpTypeInfo
{
	virtual ~TCSharpTypeInfo() = default;

	TCSharpTypeInfo(const TSharedPtr<FJsonValue>& MetaData, const TSharedPtr<FCSAssembly>& InOwningAssembly) : Field(nullptr), OwningAssembly(InOwningAssembly)
	{
		TypeMetaData = MakeShared<TMetaData>();
		TypeMetaData->SerializeFromJson(MetaData->AsObject());
	}

	TCSharpTypeInfo() : Field(nullptr)
	{
		
	}
	
	// The metadata for this type (properties, functions et.c.)
	TSharedPtr<TMetaData> TypeMetaData;

	// Pointer to the field of this type
	TField* Field;

	// Current state of the type
	ETypeState State = ETypeState::NeedRebuild;

	// Owning assembly
	TSharedPtr<FCSAssembly> OwningAssembly;

	virtual TField* InitializeBuilder()
	{
		if (Field && State == UpToDate)
        {
			// No need to rebuild or update
            return Field;
        }
		
		// Builder for this type
		TTypeBuilder TypeBuilder(TypeMetaData, OwningAssembly);
		Field = TypeBuilder.CreateType();
		
		if (State == NeedRebuild)
		{
			TypeBuilder.RebuildType();
        }
        else if (State == NeedUpdate)
        {
            TypeBuilder.UpdateType();
		}
	
		State = UpToDate;
		return Field;
	}
};
