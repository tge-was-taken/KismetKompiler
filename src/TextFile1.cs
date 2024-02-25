namespace Game.Xrd777.Battle.Critical.LS_Btl_Critical_Pc01 {
    public class LS_Btl_Critical_Pc01 : LevelSequence {}
}
namespace Game.Xrd777.Battle.Players.Pc01.AM_BtlPc01 {
    public class AM_BtlPc01 : AnimMontage {}
}
namespace Game.Xrd777.Battle.Players.Pc01.DT_BtlPc01CharacterVisual {
    public class DT_BtlPc01CharacterVisual : DataTable {}
}
namespace Game.Xrd777.Battle.Players.Pc01.DT_BtlPc01Cylinder {
    public class DT_BtlPc01Cylinder : DataTable {}
}
namespace Game.Xrd777.Blueprints.Battle.Equipments.BP_BtlSummonGun {
    public class BP_BtlSummonGun_C : BlueprintGeneratedClass {
        public ChildActorComponent ChildActor1_GEN_VARIABLE;
        public ChildActorComponent ChildActor_GEN_VARIABLE;
        public SceneComponent DefaultSceneRoot_GEN_VARIABLE;
        public SceneComponent Scene_GEN_VARIABLE;
        public ChildActorComponent ChildActor1;
        public ChildActorComponent ChildActor;
        public SceneComponent DefaultSceneRoot;
        public SceneComponent Scene;
    }
    public class Default__BP_BtlSummonGun_C : BP_BtlSummonGun_C {}
}
namespace Game.Xrd777.Blueprints.Battle.System.BP_BtlCharacterBase {
    public class BP_BtlCharacterBase_C : BlueprintGeneratedClass {
        public BP_BtlCharacterTidy_C BP_BtlCharacterTidy_GEN_VARIABLE;
        public BP_BtlResidentDataComp_C BP_BtlResidentDataComp_GEN_VARIABLE;
        public BtlSkillGeneratorComponent BtlSkillGenerator_GEN_VARIABLE;
        public CapsuleComponent CylinderCommon_GEN_VARIABLE;
        [UnknownSignature, LocalFinalFunction] public sealed void CalcAttackDelayTime(int param0);
        [UnknownSignature, LocalFinalFunction] public sealed void ``Check Need to Finailze Attack Turn``(int param0);
        [UnknownSignature, LocalFinalFunction] public sealed void GetAnimMontage(int param0);
        [UnknownSignature, LocalFinalFunction] public sealed void GetMaxRunLenForAttack(int param0);
        [UnknownSignature] public  void ``On Request Attack From Event ``();
        [UnknownSignature, LocalFinalFunction] public sealed void ``Play Specific Attack Camera``();
        [UnknownSignature, LocalFinalFunction] public sealed void PlayAttackCamera();
        [UnknownSignature, LocalFinalFunction] public sealed void PlayAttackHitSequence();
        public SceneComponent CharacterRoot_GEN_VARIABLE;
        public SceneComponent DefaultSceneRoot_GEN_VARIABLE;
        public BP_BtlCharacterTidy_C BP_BtlCharacterTidy;
        public BP_BtlResidentDataComp_C BP_BtlResidentDataComp;
        public BtlSkillGeneratorComponent BtlSkillGenerator;
        public CapsuleComponent CylinderCommon;
        public SceneComponent CharacterRoot;
        public SceneComponent DefaultSceneRoot;
    }
    public class Default__BP_BtlCharacterBase_C : BP_BtlCharacterBase_C {}
}
namespace Game.Xrd777.Blueprints.Battle.System.BP_BtlCharacterTidy {
    public class BP_BtlCharacterTidy_C : BlueprintGeneratedClass {}
}
namespace Game.Xrd777.Blueprints.Battle.System.BP_BtlEvent {
    public class BP_BtlEvent_C : BlueprintGeneratedClass {}
    public class Default__BP_BtlEvent_C : BP_BtlEvent_C {}
}
namespace Game.Xrd777.Blueprints.Battle.System.BP_BtlEventAssistant {
    public class BP_BtlEventAssistant_C : BlueprintGeneratedClass {
        public BtlBCDCharaCameraComponent BtlBCDCharaCamera_GEN_VARIABLE;
        public BtlBCDMoveCameraComponent BtlBCDMoveCamera_GEN_VARIABLE;
        public SceneComponent DefaultSceneRoot_GEN_VARIABLE;
        public BtlBCDCharaCameraComponent BtlBCDCharaCamera;
        public BtlBCDMoveCameraComponent BtlBCDMoveCamera;
        public SceneComponent DefaultSceneRoot;
    }
    public class Default__BP_BtlEventAssistant_C : BP_BtlEventAssistant_C {}
}
namespace Game.Xrd777.Blueprints.Battle.System.BP_BtlHumanBase {
    public class BP_BtlHumanBase_C : BlueprintGeneratedClass {
        public ChildActorComponent ChildActorSummonGun_GEN_VARIABLE;
        [UnknownSignature, LocalFinalFunction] public sealed void CharacterDestroy();
        [UnknownSignature, LocalFinalFunction] public sealed void CheckReadyCharacterBP();
        [UnknownSignature, LocalFinalFunction] public sealed void DestroyCharacter();
        [UnknownSignature, LocalFinalFunction] public sealed void InitAfterCreateAppCharacter();
        [UnknownSignature, LocalFinalFunction] public sealed void ReceiveBeginPlay();
        [UnknownSignature, LocalFinalFunction] public sealed void ReceiveTick(int param0);
        [UnknownSignature, LocalFinalFunction] public sealed void SetEquipVisibility(int param0, int param1, int param2, int param3);
        [UnknownSignature, LocalFinalFunction] public sealed void SetGunVisible(int param0);
        public SceneComponent DefaultSceneRoot_GEN_VARIABLE;
        public ChildActorComponent ChildActorSummonGun;
        public SceneComponent DefaultSceneRoot;
    }
    public class Default__BP_BtlHumanBase_C : BP_BtlHumanBase_C {}
}
namespace Game.Xrd777.Blueprints.Battle.System.BP_BtlResidentDataComp {
    public class BP_BtlResidentDataComp_C : BlueprintGeneratedClass {}
}
namespace Game.Xrd777.Blueprints.Battle.System.BPI_BtlCharacterContactor {
    public class BPI_BtlCharacterContactor_C : BlueprintGeneratedClass {}
    public class Default__BPI_BtlCharacterContactor_C : BPI_BtlCharacterContactor_C {}
}
namespace Game.Xrd777.Blueprints.Characters.BP_AppCharacter {
    public class BP_AppCharacter_C : BlueprintGeneratedClass {
        public AppCharacterComp AppCharacterComp_GEN_VARIABLE;
        public AppCharFootstepsAtom FootstepsAtom_GEN_VARIABLE;
        public CharacterLipsyncAppComponent CharacterLipsyncApp_GEN_VARIABLE;
        public CharacterSimpleLipsAnimAppComponent CharacterSimpleLipsAnimApp_GEN_VARIABLE;
        public SceneComponent DefaultSceneRoot_GEN_VARIABLE;
        public AppCharacterComp AppCharacterComp;
        public AppCharFootstepsAtom FootstepsAtom;
        public CharacterLipsyncAppComponent CharacterLipsyncApp;
        public CharacterSimpleLipsAnimAppComponent CharacterSimpleLipsAnimApp;
        public SceneComponent DefaultSceneRoot;
    }
    public class Default__BP_AppCharacter_C : BP_AppCharacter_C {
        public CapsuleComponent CollisionCylinder;
        public CharacterMovementComponent CharMoveComp;
        public SkeletalMeshComponent CharacterMesh0;
    }
}
namespace Game.Xrd777.Blueprints.Props.BP_Pp0901_000 {
    public class BP_Pp0901_000_C : BlueprintGeneratedClass {}
}
namespace Game.Xrd777.Blueprints.Props.BP_Pp0901_030 {
    public class BP_Pp0901_030_C : BlueprintGeneratedClass {}
}
namespace Script.CoreUObject {
    public class Function {}
    public class Object {}
    Function Default__Function;
    public class Class {}
    public class Package {}
    public class IntProperty {}
    public class ObjectProperty {}
    public class BoolProperty {}
    public class FloatProperty {}
    public class StrProperty {}
    public class DelegateProperty {}
    public class NameProperty {}
    public class ArrayProperty {}
    public class StructProperty {}
    public class SoftObjectProperty {}
}
namespace Script.Engine {
    public class Default__BlueprintGeneratedClass : BlueprintGeneratedClass {}
    public class Actor {}
    public class AnimMontage {}
    public class BlueprintGeneratedClass {}
    public class ChildActorComponent {}
    public class InheritableComponentHandler {}
    public class KismetArrayLibrary {
        [UnknownSignature, FinalFunction] public sealed void Array_Add(int param0, int param1);
        [UnknownSignature, FinalFunction] public sealed void Array_Get(int param0, int param1, int param2);
        [UnknownSignature, FinalFunction] public sealed void Array_Length(int param0);
    }
    public class KismetMathLibrary {
        [UnknownSignature, MathFunction] public static sealed void Add_IntInt(int param0, int param1);
        [UnknownSignature, MathFunction] public static sealed void BooleanAND(int param0, int param1);
        [UnknownSignature, MathFunction] public static sealed void BooleanOR(int param0, int param1);
        [UnknownSignature, MathFunction] public static sealed void EqualEqual_IntInt(int param0, int param1);
        [UnknownSignature, MathFunction] public static sealed void EqualEqual_NameName(int param0, int param1);
        [UnknownSignature, MathFunction] public static sealed void GreaterEqual_IntInt(int param0, int param1);
        [UnknownSignature, MathFunction] public static sealed void Less_IntInt(int param0, int param1);
        [UnknownSignature, MathFunction] public static sealed void Not_PreBool(int param0);
        [UnknownSignature, MathFunction] public static sealed void SelectFloat(int param0, int param1, int param2);
    }
    public class KismetStringLibrary {
        [UnknownSignature, MathFunction] public static sealed void Conv_StringToInt(int param0);
        [UnknownSignature, MathFunction] public static sealed void Split(int param0, int param1, int param2, int param3, int param4, int param5);
    }
    public class KismetSystemLibrary {
        public object OnAssetLoaded__DelegateSignature;
        [UnknownSignature, MathFunction] public static sealed void GetObjectName(int param0);
        [UnknownSignature, MathFunction] public static sealed void IsValid(int param0);
        [UnknownSignature, MathFunction] public static sealed void LoadAsset(int param0, int param1, int param2, int param3);
    }
    public class SceneComponent {}
    public class SCS_Node {}
    public class SimpleConstructionScript {}
    public class SkeletalMeshComponent {}
    public class SkinnedMeshComponent {}
    public class Default__InheritableComponentHandler : InheritableComponentHandler {}
    public class Default__KismetArrayLibrary : KismetArrayLibrary {}
    public class Default__SceneComponent : SceneComponent {}
    public class LatentActionInfo {}
    public class PointerToUberGraphFrame {}
    public class Default__SCS_Node : SCS_Node {}
    public class Default__SimpleConstructionScript : SimpleConstructionScript {}
    public class CapsuleComponent {}
    public class CharacterMovementComponent {}
    public class DataTable {}
}
namespace Script.LevelSequence {
    public class LevelSequence {}
    public class LevelSequenceActor {
        [UnknownSignature, FinalFunction] public sealed void GetSequencePlayer();
    }
    public class LevelSequencePlayer {
        [UnknownSignature, MathFunction] public static sealed void CreateLevelSequencePlayer(int param0, int param1, int param2, int param3);
    }
}
namespace Script.MovieScene {
    public class MovieSceneSequencePlayer {
        [UnknownSignature, FinalFunction] public sealed void Play();
    }
    public class OnMovieSceneSequencePlayerEvent__DelegateSignature {}
    public class MovieSceneSequenceLoopCount {}
    public class MovieSceneSequencePlaybackSettings {}
}
namespace Script.xrd777 {
    public class AppCharWeaponBase {}
    public class BtlActor {
        public object SpawnCharacterBP;
    }
    public class BtlBossNyxCoreInterface {}
    public class AppCharacterComp {}
    public class AppCharFootstepsAtom {}
    public class BtlBCDCharaCameraComponent {}
    public class BtlBCDMoveCameraComponent {}
    public class BtlSkillGeneratorComponent {}
    public class CharacterLipsyncAppComponent {}
    public class CharacterSimpleLipsAnimAppComponent {}
}

using Game.Xrd777.Battle.Critical.LS_Btl_Critical_Pc01;
using Game.Xrd777.Battle.Players.Pc01.AM_BtlPc01;
using Game.Xrd777.Battle.Players.Pc01.DT_BtlPc01CharacterVisual;
using Game.Xrd777.Battle.Players.Pc01.DT_BtlPc01Cylinder;
using Game.Xrd777.Blueprints.Battle.Equipments.BP_BtlSummonGun;
using Game.Xrd777.Blueprints.Battle.System.BP_BtlCharacterBase;
using Game.Xrd777.Blueprints.Battle.System.BP_BtlCharacterTidy;
using Game.Xrd777.Blueprints.Battle.System.BP_BtlEvent;
using Game.Xrd777.Blueprints.Battle.System.BP_BtlEventAssistant;
using Game.Xrd777.Blueprints.Battle.System.BP_BtlHumanBase;
using Game.Xrd777.Blueprints.Battle.System.BP_BtlResidentDataComp;
using Game.Xrd777.Blueprints.Battle.System.BPI_BtlCharacterContactor;
using Game.Xrd777.Blueprints.Characters.BP_AppCharacter;
using Game.Xrd777.Blueprints.Props.BP_Pp0901_000;
using Game.Xrd777.Blueprints.Props.BP_Pp0901_030;
using Script.CoreUObject;
using Script.Engine;
using Script.LevelSequence;
using Script.MovieScene;
using Script.xrd777;

[Config, Parsed, ReplicationDataIsSetUp, CompiledFromBlueprint, HasInstancedReference]
class BP_BtlPc01_C : BP_BtlHumanBase_C {
    [Transient, DuplicateTransient] Struct<PointerToUberGraphFrame> UberGraphFrame;
    [Edit, BlueprintVisible, DisableEditOnInstance] SoftObject AnimMontageNyxCoreAsset;
    [Edit, BlueprintVisible, DisableEditOnInstance] Object<AnimMontage> AnimMontageNyxCore;
    [Edit, BlueprintVisible, DisableEditOnInstance] bool IsLoadedNyxCoreStaff;
    [Edit, BlueprintVisible, DisableEditOnInstance] bool IsRequestAdditionalAnim;
    [Edit, BlueprintVisible, DisableEditOnInstance] bool IsNyxCoreMode;
    [Edit, BlueprintVisible, DisableEditOnInstance] SoftObject AttackBCDNyxCore_1;
    [Edit, BlueprintVisible, DisableEditOnInstance] SoftObject AttackBCDNyxCore_2;
    [Edit, BlueprintVisible, DisableEditOnInstance] int LoadedCount;
    [Edit, BlueprintVisible, DisableEditOnInstance] int LoadRequestCount;
    [Edit, BlueprintVisible, DisableEditOnInstance, UObjectWrapper] Object<LevelSequence> AttackBCD_1;
    [Edit, BlueprintVisible, DisableEditOnInstance, UObjectWrapper] Object<LevelSequence> AttackBCD_2;
    [Edit, BlueprintVisible, DisableEditOnInstance] int AttackCountToNyxCore;
    [Edit, BlueprintVisible, DisableEditOnTemplate, DisableEditOnInstance] Object<LevelSequenceActor> AttackBCD;
    [Edit, BlueprintVisible, DisableEditOnInstance] bool isAttackCameraSyncMode;
    [UbergraphFunction, FuncOverrideMatch]
    sealed void ExecuteUbergraph_BP_BtlPc01([BlueprintVisible, BlueprintReadOnly] int EntryPoint) {
        // Locals
        Object<Object> Temp_object_Variable;
        Object<Object> K2Node_CustomEvent_Loaded;
        Object<LevelSequence> K2Node_DynamicCast_AsLevel_Sequence;
        bool K2Node_DynamicCast_bSuccess;
        int Temp_int_Variable;
        float K2Node_Event_DeltaSeconds;
        bool K2Node_Event_initialHiding;
        string CallFunc_GetObjectName_ReturnValue;
        string CallFunc_Split_LeftS;
        string CallFunc_Split_RightS;
        bool CallFunc_Split_ReturnValue;
        Object<Object> Temp_object_Variable_1;
        int CallFunc_Conv_StringToInt_ReturnValue;
        Object<LevelSequence> K2Node_DynamicCast_AsLevel_Sequence_1;
        bool K2Node_DynamicCast_bSuccess_1;
        Object<Object> K2Node_CustomEvent_Loaded_1;
        int CallFunc_Add_IntInt_ReturnValue;
        Object<Object> Temp_object_Variable_2;
        Object<AnimMontage> K2Node_DynamicCast_AsAnim_Montage;
        bool K2Node_DynamicCast_bSuccess_2;
        bool CallFunc_GreaterEqual_IntInt_ReturnValue;
        bool CallFunc_IsValid_ReturnValue;
        Object<Object> K2Node_CustomEvent_Loaded_2;
        bool CallFunc_IsValid_ReturnValue_1;
        float K2Node_Event_StartAnimationTime;
        [InstancedReference] Object<SkeletalMeshComponent> CallFunc_GetMesh_ReturnValue;
        Delegate K2Node_CreateDelegate_OutputDelegate;
        [InstancedReference] Object<SkeletalMeshComponent> CallFunc_GetMesh_ReturnValue_1;
        Delegate K2Node_CreateDelegate_OutputDelegate_1;
        Delegate K2Node_CreateDelegate_OutputDelegate_2;

        while (true) {
            // Block 1
            goto EntryPoint;

            // Block 2
            ExecuteUbergraph_BP_BtlPc01_15: this.FinalizeAttackRun();
            this.``On Request Attack Animation``(true);
            EX_PopExecutionFlow();

            // Block 3
            ExecuteUbergraph_BP_BtlPc01_45: EX_LetObj(Temp_object_Variable,K2Node_CustomEvent_Loaded);
            K2Node_DynamicCast_AsLevel_Sequence = EX_DynamicCast("LevelSequence", Temp_object_Variable);
            K2Node_DynamicCast_bSuccess = EX_PrimitiveCast("ObjectToBool", K2Node_DynamicCast_AsLevel_Sequence);
            EX_PopExecutionFlowIfNot(K2Node_DynamicCast_bSuccess);

            // Block 4
            EX_LetObj(this.AttackBCD_2,K2Node_DynamicCast_AsLevel_Sequence);

            // Block 5
            ExecuteUbergraph_BP_BtlPc01_158: CallFunc_Add_IntInt_ReturnValue = Add_IntInt(this.LoadedCount, 1);
            Temp_int_Variable = CallFunc_Add_IntInt_ReturnValue;
            this.LoadedCount = Temp_int_Variable;
            CallFunc_GreaterEqual_IntInt_ReturnValue = (bool)(GreaterEqual_IntInt(this.LoadedCount, this.LoadRequestCount));
            EX_PopExecutionFlowIfNot(CallFunc_GreaterEqual_IntInt_ReturnValue);

            // Block 6
            this.IsLoadedNyxCoreStaff = (bool)(true);
            EX_PopExecutionFlow();

            // Block 7
            ExecuteUbergraph_BP_BtlPc01_314: EX_BindDelegate("OnLoaded_93CFD2164DB0B8B6AC8D1991510E30E6", K2Node_CreateDelegate_OutputDelegate_1, this);
            LoadAsset(this, this.AttackBCDNyxCore_1, K2Node_CreateDelegate_OutputDelegate_1, EX_StructConst(LatentActionInfo, 32, -1, -1038822020, EX_NameConst("ExecuteUbergraph_BP_BtlPc01"), this));
            EX_PopExecutionFlow();

            // Block 8
            ExecuteUbergraph_BP_BtlPc01_405: BP_BtlPc01_C.ReceiveBeginPlay();
            EX_PopExecutionFlow();

            // Block 9
            ExecuteUbergraph_BP_BtlPc01_416: BP_BtlPc01_C.ReceiveTick(K2Node_Event_DeltaSeconds);
            EX_PopExecutionFlowIfNot(this.isAttackCameraSyncMode);

            // Block 10
            this.BtlEvent.``Event Assistant``.BtlCamera.SyncEventCamToSystemCam();
            EX_PopExecutionFlow();

            // Block 11
            ExecuteUbergraph_BP_BtlPc01_526: CallFunc_GetObjectName_ReturnValue = GetObjectName(this);
            CallFunc_Split_ReturnValue = (bool)(Split(CallFunc_GetObjectName_ReturnValue, "Pc", CallFunc_Split_LeftS, CallFunc_Split_RightS, (byte)(1), (byte)(0)));
            CallFunc_Conv_StringToInt_ReturnValue = Conv_StringToInt(CallFunc_Split_RightS);
            this.CreateCharacter(CallFunc_Conv_StringToInt_ReturnValue);
            EX_PopExecutionFlow();

            // Block 12
            ExecuteUbergraph_BP_BtlPc01_671: this.IsRequestAdditionalAnim = (bool)(true);
            this.IsNyxCoreMode = (bool)(true);
            this.IsLoadedNyxCoreStaff = (bool)(false);
            this.LoadRequestCount = 3;
            EX_PushExecutionFlow(ExecuteUbergraph_BP_BtlPc01_828);

            // Block 13
            EX_PushExecutionFlow(ExecuteUbergraph_BP_BtlPc01_314);

            // Block 14
            EX_BindDelegate("OnLoaded_80B839724B03E403FBB8C4ADFCF16D40", K2Node_CreateDelegate_OutputDelegate_2, this);
            LoadAsset(this, this.AnimMontageNyxCoreAsset, K2Node_CreateDelegate_OutputDelegate_2, EX_StructConst(LatentActionInfo, 32, -1, -399838087, EX_NameConst("ExecuteUbergraph_BP_BtlPc01"), this));
            EX_PopExecutionFlow();

            // Block 15
            ExecuteUbergraph_BP_BtlPc01_828: EX_BindDelegate("OnLoaded_9AB6DCDD401E222E4648B982796CF8B6", K2Node_CreateDelegate_OutputDelegate, this);
            LoadAsset(this, this.AttackBCDNyxCore_2, K2Node_CreateDelegate_OutputDelegate, EX_StructConst(LatentActionInfo, 32, -1, 1359081058, EX_NameConst("ExecuteUbergraph_BP_BtlPc01"), this));
            EX_PopExecutionFlow();

            // Block 16
            ExecuteUbergraph_BP_BtlPc01_919: K2Node_DynamicCast_AsLevel_Sequence_1 = EX_DynamicCast("LevelSequence", Temp_object_Variable_1);
            K2Node_DynamicCast_bSuccess_1 = EX_PrimitiveCast("ObjectToBool", K2Node_DynamicCast_AsLevel_Sequence_1);
            EX_PopExecutionFlowIfNot(K2Node_DynamicCast_bSuccess_1);

            // Block 17
            EX_LetObj(this.AttackBCD_1,K2Node_DynamicCast_AsLevel_Sequence_1);
            goto ExecuteUbergraph_BP_BtlPc01_158;

            // Block 18
            ExecuteUbergraph_BP_BtlPc01_1018: EX_LetObj(Temp_object_Variable_1,K2Node_CustomEvent_Loaded_1);
            goto ExecuteUbergraph_BP_BtlPc01_919;

            // Block 19
            ExecuteUbergraph_BP_BtlPc01_1042: K2Node_DynamicCast_AsAnim_Montage = EX_DynamicCast("AnimMontage", Temp_object_Variable_2);
            K2Node_DynamicCast_bSuccess_2 = EX_PrimitiveCast("ObjectToBool", K2Node_DynamicCast_AsAnim_Montage);
            EX_PopExecutionFlowIfNot(K2Node_DynamicCast_bSuccess_2);

            // Block 20
            EX_LetObj(this.AnimMontageNyxCore,K2Node_DynamicCast_AsAnim_Montage);
            goto ExecuteUbergraph_BP_BtlPc01_158;

            ExecuteUbergraph_BP_BtlPc01_1141:
            while (true) {
                while (true) {
                    // Block 21
                    this.BtlEvent.``Event Assistant``.Invoke.Clear();
                    break;

                }


                // Block 22
                ExecuteUbergraph_BP_BtlPc01_1232: CallFunc_IsValid_ReturnValue = (bool)(IsValid(this.AttackBCD));
                if (!CallFunc_IsValid_ReturnValue) break;

                // Block 23
                this.isAttackCameraSyncMode = (bool)(false);
                this.BtlEvent.``Event Assistant``.BtlCamera.SyncEventCamToSystemCam();
                this.AttackBCD.K2_DestroyActor();
                break;

            }


            // Block 24
            ExecuteUbergraph_BP_BtlPc01_1399: this.``Has Finished Attack From Event``();
            EX_PopExecutionFlow();

            // Block 25
            ExecuteUbergraph_BP_BtlPc01_1414: BP_BtlPc01_C.CharacterDestroy();
            EX_PopExecutionFlowIfNot(this.IsNyxCoreMode);

            while (true) {
                // Block 26
                EX_LetObj(this.AttackBCD_1,EX_NoObject());
                EX_LetObj(this.AttackBCD_2,EX_NoObject());
                break;

            }


            // Block 27
            ExecuteUbergraph_BP_BtlPc01_1462: CallFunc_IsValid_ReturnValue_1 = (bool)(IsValid(this.AttackBCD));
            EX_PopExecutionFlowIfNot(CallFunc_IsValid_ReturnValue_1);

            // Block 28
            this.AttackBCD.K2_DestroyActor();
            EX_PopExecutionFlow();

            // Block 29
            ExecuteUbergraph_BP_BtlPc01_1538: EX_LetObj(Temp_object_Variable_2,K2Node_CustomEvent_Loaded_2);
            goto ExecuteUbergraph_BP_BtlPc01_1042;

            // Block 30
            ExecuteUbergraph_BP_BtlPc01_1562: EX_PopExecutionFlowIfNot(this.IsNyxCoreMode);

            // Block 31
            this.SetupEventAnimation(true);
            goto ExecuteUbergraph_BP_BtlPc01_15;

            // Block 32
            ExecuteUbergraph_BP_BtlPc01_1592: EX_LetObj(CallFunc_GetMesh_ReturnValue,this.AppCharacter.AppCharacterComp.GetMesh());
            CallFunc_GetMesh_ReturnValue.VisibilityBasedAnimTickOption = (byte)(1);
            this.RequestAnimation((byte)(57), 0f, false, 0f);
            EX_PopExecutionFlow();

            // Block 33
            ExecuteUbergraph_BP_BtlPc01_1730: EX_LetObj(CallFunc_GetMesh_ReturnValue_1,this.AppCharacter.AppCharacterComp.GetMesh());
            CallFunc_GetMesh_ReturnValue_1.VisibilityBasedAnimTickOption = (byte)(0);
            this.RequestAnimation((byte)(21), K2Node_Event_StartAnimationTime, true, 0f);
            this.SetWeaponVisible(true);
            EX_PopExecutionFlow();

        }


        // Block 34
        ExecuteUbergraph_BP_BtlPc01_1887:

    }

    [AccessSpecifiers, FuncOverrideMatch, BlueprintCallable, BlueprintEvent, FuncInherit]
    public void EncountHeroRunStart([BlueprintVisible, BlueprintReadOnly] float StartAnimationTime) {
        // Block 1
        EX_LetValueOnPersistentFrame("K2Node_Event_StartAnimationTime", StartAnimationTime);
        this.ExecuteUbergraph_BP_BtlPc01(ExecuteUbergraph_BP_BtlPc01_1730);

    }

    [AccessSpecifiers, FuncOverrideMatch, BlueprintCallable, BlueprintEvent, FuncInherit]
    public void EncountHeroRunStop() {
        // Block 1
        this.ExecuteUbergraph_BP_BtlPc01(ExecuteUbergraph_BP_BtlPc01_1592);

    }

    [BlueprintCallable, BlueprintEvent, FuncInherit]
    override void ``On Request Attack From Event ``() {
        // Block 1
        this.ExecuteUbergraph_BP_BtlPc01(ExecuteUbergraph_BP_BtlPc01_1562);

    }

    [Event, AccessSpecifiers, FuncOverrideMatch, BlueprintEvent, FuncInherit]
    public override void CharacterDestroy() {
        // Block 1
        this.ExecuteUbergraph_BP_BtlPc01(ExecuteUbergraph_BP_BtlPc01_1414);

    }

    [BlueprintCallable, BlueprintEvent, FuncInherit]
    void ``Has Finished Attack BCD to NyxCore``() {
        // Block 1
        this.ExecuteUbergraph_BP_BtlPc01(ExecuteUbergraph_BP_BtlPc01_1141);

    }

    [Event, AccessSpecifiers, FuncOverrideMatch, BlueprintCallable, BlueprintEvent, FuncInherit]
    public void LoadHeroAnimationForNyxCore() {
        // Block 1
        this.ExecuteUbergraph_BP_BtlPc01(ExecuteUbergraph_BP_BtlPc01_671);

    }

    [Event, AccessSpecifiers, FuncOverrideMatch, BlueprintCallable, BlueprintEvent, FuncInherit]
    public override void SpawnCharacterBP([BlueprintVisible, BlueprintReadOnly] bool initialHiding) {
        // Block 1
        EX_LetValueOnPersistentFrame("K2Node_Event_initialHiding", initialHiding);
        this.ExecuteUbergraph_BP_BtlPc01(ExecuteUbergraph_BP_BtlPc01_526);

    }

    [Event, AccessSpecifiers, FuncOverrideMatch, BlueprintEvent, FuncInherit]
    public override void ReceiveTick([BlueprintVisible, BlueprintReadOnly] float DeltaSeconds) {
        // Block 1
        EX_LetValueOnPersistentFrame("K2Node_Event_DeltaSeconds", DeltaSeconds);
        this.ExecuteUbergraph_BP_BtlPc01(ExecuteUbergraph_BP_BtlPc01_416);

    }

    [Event, AccessSpecifiers, FuncOverrideMatch, BlueprintEvent, FuncInherit]
    protected override void ReceiveBeginPlay() {
        // Block 1
        this.ExecuteUbergraph_BP_BtlPc01(ExecuteUbergraph_BP_BtlPc01_405);

    }

    [BlueprintCallable, BlueprintEvent, FuncInherit]
    void OnLoaded_9AB6DCDD401E222E4648B982796CF8B6([BlueprintVisible, BlueprintReadOnly] Object<Object> Loaded) {
        // Block 1
        EX_LetValueOnPersistentFrame("K2Node_CustomEvent_Loaded", Loaded);
        this.ExecuteUbergraph_BP_BtlPc01(ExecuteUbergraph_BP_BtlPc01_45);

    }

    [BlueprintCallable, BlueprintEvent, FuncInherit]
    void OnLoaded_93CFD2164DB0B8B6AC8D1991510E30E6([BlueprintVisible, BlueprintReadOnly] Object<Object> Loaded) {
        // Block 1
        EX_LetValueOnPersistentFrame("K2Node_CustomEvent_Loaded_1", Loaded);
        this.ExecuteUbergraph_BP_BtlPc01(ExecuteUbergraph_BP_BtlPc01_1018);

    }

    [BlueprintCallable, BlueprintEvent, FuncInherit]
    void OnLoaded_80B839724B03E403FBB8C4ADFCF16D40([BlueprintVisible, BlueprintReadOnly] Object<Object> Loaded) {
        // Block 1
        EX_LetValueOnPersistentFrame("K2Node_CustomEvent_Loaded_2", Loaded);
        this.ExecuteUbergraph_BP_BtlPc01(ExecuteUbergraph_BP_BtlPc01_1538);

    }

    [Event, AccessSpecifiers, FuncOverrideMatch, HasOutParms, BlueprintCallable, BlueprintEvent, FuncInherit]
    public override void CheckReadyCharacterBP([Return] out bool ReturnValue) {
        // Locals
        bool CallFunc_BooleanAND_ReturnValue;
        bool CallFunc_Not_PreBool_ReturnValue;
        bool CallFunc_CheckReadyCharacterBP_ReturnValue;
        bool CallFunc_BooleanOR_ReturnValue;
        bool CallFunc_BooleanAND_ReturnValue_1;

        // Block 1
        CallFunc_CheckReadyCharacterBP_ReturnValue = (bool)(BP_BtlPc01_C.CheckReadyCharacterBP());
        CallFunc_BooleanAND_ReturnValue = (bool)(BooleanAND(this.IsNyxCoreMode, this.IsLoadedNyxCoreStaff));
        CallFunc_Not_PreBool_ReturnValue = (bool)(Not_PreBool(this.IsRequestAdditionalAnim));
        CallFunc_BooleanOR_ReturnValue = (bool)(BooleanOR(CallFunc_Not_PreBool_ReturnValue, CallFunc_BooleanAND_ReturnValue));
        CallFunc_BooleanAND_ReturnValue_1 = (bool)(BooleanAND(CallFunc_CheckReadyCharacterBP_ReturnValue, CallFunc_BooleanOR_ReturnValue));
        ReturnValue = (bool)(CallFunc_BooleanAND_ReturnValue_1);
        return ReturnValue;

    }

    [AccessSpecifiers, FuncOverrideMatch, BlueprintCallable, BlueprintEvent, FuncInherit]
    public override void DestroyCharacter() {
        // Locals
        bool CallFunc_IsValid_ReturnValue;

        // Block 1
        BP_BtlPc01_C.DestroyCharacter();
        CallFunc_IsValid_ReturnValue = (bool)(IsValid(this.AnimMontageNyxCore));
        if (!CallFunc_IsValid_ReturnValue) return;

        // Block 2
        EX_LetObj(this.AnimMontageNyxCore,EX_NoObject());

        // Block 3
        DestroyCharacter_64:

    }

    [AccessSpecifiers, FuncOverrideMatch, HasOutParms, BlueprintCallable, BlueprintEvent, BlueprintPure, FuncInherit]
    protected override void GetAnimMontage(out Object<AnimMontage> ``Anim Montage``) {
        // Locals
        Object<AnimMontage> CallFunc_GetAnimMontage_Anim_Montage;

        // Block 1

        if (this.IsNyxCoreMode) {
            // Block 2
            EX_LetObj(``Anim Montage``,this.AnimMontageNyxCore);
            return;

        }


        // Block 3
        GetAnimMontage_38: BP_BtlPc01_C.GetAnimMontage(CallFunc_GetAnimMontage_Anim_Montage);
        EX_LetObj(``Anim Montage``,CallFunc_GetAnimMontage_Anim_Montage);

        // Block 4
        GetAnimMontage_76:

    }

    [AccessSpecifiers, FuncOverrideMatch, BlueprintCallable, BlueprintEvent, FuncInherit]
    protected override void PlayAttackCamera() {
        // Block 1

        if (this.IsNyxCoreMode) {
            // Block 2
            return;

        }


        // Block 3
        PlayAttackCamera_19: BP_BtlPc01_C.PlayAttackCamera();

        // Block 4
        PlayAttackCamera_29:

    }

    [AccessSpecifiers, FuncOverrideMatch, BlueprintCallable, BlueprintEvent, FuncInherit]
    protected override void ``Play Specific Attack Camera``() {
        // Locals
        int CallFunc_Add_IntInt_ReturnValue;
        Object<LevelSequenceActor> CallFunc_CreateLevelSequencePlayer_OutActor;
        Object<LevelSequencePlayer> CallFunc_CreateLevelSequencePlayer_ReturnValue;
        Delegate K2Node_CreateDelegate_OutputDelegate;
        int Temp_int_Variable;
        Object<LevelSequencePlayer> CallFunc_GetSequencePlayer_ReturnValue;
        Object<LevelSequencePlayer> CallFunc_GetSequencePlayer_ReturnValue_1;
        bool CallFunc_IsValid_ReturnValue;
        Object<LevelSequenceActor> CallFunc_CreateLevelSequencePlayer_OutActor_1;
        Object<LevelSequencePlayer> CallFunc_CreateLevelSequencePlayer_ReturnValue_1;
        bool CallFunc_EqualEqual_IntInt_ReturnValue;

        // Block 1
        this.``Play Specific Attack Camera``();
        if (!this.IsNyxCoreMode) return;

        // Block 2
        CallFunc_IsValid_ReturnValue = (bool)(IsValid(this.AttackBCD));

        if (CallFunc_IsValid_ReturnValue) {
            // Block 3
            this.AttackBCD.K2_DestroyActor();

        }


        // Block 4
        ``Play Specific Attack Camera_103``: CallFunc_EqualEqual_IntInt_ReturnValue = (bool)(EqualEqual_IntInt(this.AttackCountToNyxCore, 0));

        if (CallFunc_EqualEqual_IntInt_ReturnValue) {
            // Block 5
            EX_LetObj(CallFunc_CreateLevelSequencePlayer_ReturnValue_1,CreateLevelSequencePlayer(this, this.AttackBCD_1, EX_StructConst(MovieSceneSequencePlaybackSettings, 20, false, EX_StructConst(MovieSceneSequenceLoopCount, 4, 0), 1f, 0f, false, false, false, false, false, false, false, false), CallFunc_CreateLevelSequencePlayer_OutActor_1));
            EX_LetObj(this.AttackBCD,CallFunc_CreateLevelSequencePlayer_OutActor_1);

            // Block 6
            ``Play Specific Attack Camera_261``: this.BtlEvent.``Event Assistant``.Invoke.SetCharacter(this);
            EX_BindDelegate("Has Finished Attack BCD to NyxCore", K2Node_CreateDelegate_OutputDelegate, this);
            EX_LetObj(CallFunc_GetSequencePlayer_ReturnValue,this.AttackBCD.GetSequencePlayer());
            EX_AddMulticastDelegate(CallFunc_GetSequencePlayer_ReturnValue.OnFinished, K2Node_CreateDelegate_OutputDelegate);
            EX_LetObj(CallFunc_GetSequencePlayer_ReturnValue_1,this.AttackBCD.GetSequencePlayer());
            CallFunc_GetSequencePlayer_ReturnValue_1.Play();
            CallFunc_Add_IntInt_ReturnValue = Add_IntInt(this.AttackCountToNyxCore, 1);
            Temp_int_Variable = CallFunc_Add_IntInt_ReturnValue;
            this.AttackCountToNyxCore = Temp_int_Variable;
            this.isAttackCameraSyncMode = (bool)(true);
            return;

        }


        // Block 7
        ``Play Specific Attack Camera_634``: EX_LetObj(CallFunc_CreateLevelSequencePlayer_ReturnValue,CreateLevelSequencePlayer(this, this.AttackBCD_2, EX_StructConst(MovieSceneSequencePlaybackSettings, 20, false, EX_StructConst(MovieSceneSequenceLoopCount, 4, 0), 1f, 0f, false, false, false, false, false, false, false, false), CallFunc_CreateLevelSequencePlayer_OutActor));
        EX_LetObj(this.AttackBCD,CallFunc_CreateLevelSequencePlayer_OutActor);
        goto ``Play Specific Attack Camera_261``;

        // Block 8
        ``Play Specific Attack Camera_749``:

    }

    [AccessSpecifiers, FuncOverrideMatch, HasOutParms, BlueprintCallable, BlueprintEvent, BlueprintPure, FuncInherit]
    protected override void GetMaxRunLenForAttack(out float Length) {
        // Locals
        float CallFunc_GetMaxRunLenForAttack_Length;
        float CallFunc_SelectFloat_ReturnValue;

        // Block 1
        BP_BtlPc01_C.GetMaxRunLenForAttack(CallFunc_GetMaxRunLenForAttack_Length);
        CallFunc_SelectFloat_ReturnValue = SelectFloat(2000f, CallFunc_GetMaxRunLenForAttack_Length, this.IsNyxCoreMode);
        Length = CallFunc_SelectFloat_ReturnValue;

    }

    [AccessSpecifiers, FuncOverrideMatch, BlueprintCallable, BlueprintEvent, FuncInherit]
    protected override void PlayAttackHitSequence() {
        // Block 1

        if (this.IsNyxCoreMode) {
            // Block 2
            return;

        }


        // Block 3
        PlayAttackHitSequence_19: BP_BtlPc01_C.PlayAttackHitSequence();

        // Block 4
        PlayAttackHitSequence_29:

    }

    [AccessSpecifiers, FuncOverrideMatch, HasOutParms, BlueprintCallable, BlueprintEvent, BlueprintPure, FuncInherit]
    protected override void ``Check Need to Finailze Attack Turn``(out bool finazlie) {
        // Locals
        bool CallFunc_Check_Need_to_Finailze_Attack_Turn_finazlie;

        // Block 1

        if (this.IsNyxCoreMode) {
            // Block 2
            finazlie = (bool)(false);
            return;

        }


        // Block 3
        ``Check Need to Finailze Attack Turn_30``: this.``Check Need to Finailze Attack Turn``(CallFunc_Check_Need_to_Finailze_Attack_Turn_finazlie);
        finazlie = (bool)(CallFunc_Check_Need_to_Finailze_Attack_Turn_finazlie);

        // Block 4
        ``Check Need to Finailze Attack Turn_68``:

    }

    [AccessSpecifiers, FuncOverrideMatch, BlueprintCallable, BlueprintEvent, FuncInherit]
    public override void SetGunVisible([BlueprintVisible, BlueprintReadOnly] bool Visible) {
        // Block 1

        if (this.IsNyxCoreMode) {
            // Block 2
            return;

        }


        // Block 3
        SetGunVisible_19: BP_BtlPc01_C.SetGunVisible(Visible);

        // Block 4
        SetGunVisible_38:

    }

    [AccessSpecifiers, FuncOverrideMatch, HasOutParms, BlueprintCallable, BlueprintEvent, BlueprintPure, FuncInherit]
    protected override void CalcAttackDelayTime(out float Delay) {
        // Locals
        bool CallFunc_EqualEqual_IntInt_ReturnValue;
        float CallFunc_CalcAttackDelayTime_Delay;

        // Block 1

        if (this.IsNyxCoreMode) {
            // Block 2
            CallFunc_EqualEqual_IntInt_ReturnValue = (bool)(EqualEqual_IntInt(this.AttackCountToNyxCore, 0));
            if (!(CallFunc_EqualEqual_IntInt_ReturnValue)) goto CalcAttackDelayTime_141;

            // Block 3
            Delay = 0.3f;
            return;

        }


        // Block 4
        CalcAttackDelayTime_90: BP_BtlPc01_C.CalcAttackDelayTime(CallFunc_CalcAttackDelayTime_Delay);
        Delay = CallFunc_CalcAttackDelayTime_Delay;
        return;

        // Block 5
        CalcAttackDelayTime_141: Delay = 0.32f;

        // Block 6
        CalcAttackDelayTime_164:

    }

    [AccessSpecifiers, FuncOverrideMatch, BlueprintCallable, BlueprintEvent, FuncInherit]
    public override void SetEquipVisibility([BlueprintVisible, BlueprintReadOnly] Name AnimSection, [BlueprintVisible, BlueprintReadOnly] bool ForceHide, [BlueprintVisible, BlueprintReadOnly] bool ForceShowWeapon, [BlueprintVisible, BlueprintReadOnly] bool ForceHideWeapon) {
        // Locals
        bool CallFunc_EqualEqual_NameName_ReturnValue;
        bool CallFunc_BooleanOR_ReturnValue;

        // Block 1

        if (this.IsNyxCoreMode) {
            // Block 2
            CallFunc_EqualEqual_NameName_ReturnValue = (bool)(EqualEqual_NameName(AnimSection, EX_NameConst("SummonStart")));
            CallFunc_BooleanOR_ReturnValue = (bool)(BooleanOR(CallFunc_EqualEqual_NameName_ReturnValue, false));
            if (!(CallFunc_BooleanOR_ReturnValue)) goto SetEquipVisibility_127;

            // Block 3
            BP_BtlPc01_C.SetEquipVisibility(AnimSection, true, false, false);
            return;

        }


        // Block 4
        SetEquipVisibility_127: BP_BtlPc01_C.SetEquipVisibility(AnimSection, ForceHide, ForceShowWeapon, ForceHideWeapon);

        // Block 5
        SetEquipVisibility_173:

    }

    [AccessSpecifiers, FuncOverrideMatch, BlueprintCallable, BlueprintEvent, FuncInherit]
    public override void InitAfterCreateAppCharacter() {
        // Block 1
        BP_BtlPc01_C.InitAfterCreateAppCharacter();
        if (!this.IsNyxCoreMode) return;

        // Block 2
        this.SetupEventAnimation(true);

        // Block 3
        InitAfterCreateAppCharacter_39:

    }

    [AccessSpecifiers, FuncOverrideMatch, HasOutParms, BlueprintCallable, BlueprintEvent, FuncInherit]
    public void CheckNyxCoreMode(out bool NyxCoreMode) {
        // Block 1
        NyxCoreMode = (bool)(this.IsNyxCoreMode);

    }

    [AccessSpecifiers, FuncOverrideMatch, HasOutParms, HasDefaults, BlueprintCallable, BlueprintEvent, FuncInherit]
    public void EncountHeroGetShowActor(out Array<Object<Actor>> ShowActors) {
        // Locals
        [Edit, BlueprintVisible, DisableEditOnTemplate] Array<Object<Actor>> ActorList;
        int Temp_int_Array_Index_Variable;
        int Temp_int_Loop_Counter_Variable;
        int CallFunc_Add_IntInt_ReturnValue;
        Object<Actor> CallFunc_GetShowActor_Showing_Actor;
        int CallFunc_Array_Add_ReturnValue;
        ref Array<Object<AppCharWeaponBase>> CallFunc_GetWeaponList_Weapon;
        int CallFunc_Array_Length_ReturnValue;
        Object<AppCharWeaponBase> CallFunc_Array_Get_Item;
        bool CallFunc_Less_IntInt_ReturnValue;
        int CallFunc_Array_Add_ReturnValue_1;

        while (true) {
            // Block 1
            this.GetShowActor(CallFunc_GetShowActor_Showing_Actor);
            CallFunc_Array_Add_ReturnValue = Default__KismetArrayLibrary.Array_Add(ActorList, CallFunc_GetShowActor_Showing_Actor);
            this.GetWeaponList(CallFunc_GetWeaponList_Weapon);
            Temp_int_Loop_Counter_Variable = 0;
            Temp_int_Array_Index_Variable = 0;

            // Block 2
            EncountHeroGetShowActor_165: CallFunc_Array_Length_ReturnValue = Default__KismetArrayLibrary.Array_Length(CallFunc_GetWeaponList_Weapon);
            CallFunc_Less_IntInt_ReturnValue = (bool)(Less_IntInt(Temp_int_Loop_Counter_Variable, CallFunc_Array_Length_ReturnValue));

            if (CallFunc_Less_IntInt_ReturnValue) {
                // Block 3
                Temp_int_Array_Index_Variable = Temp_int_Loop_Counter_Variable;
                EX_PushExecutionFlow(EncountHeroGetShowActor_468);

                // Block 4
                Default__KismetArrayLibrary.Array_Get(CallFunc_GetWeaponList_Weapon, Temp_int_Array_Index_Variable, CallFunc_Array_Get_Item);
                CallFunc_Array_Add_ReturnValue_1 = Default__KismetArrayLibrary.Array_Add(ActorList, CallFunc_Array_Get_Item);
                EX_PopExecutionFlow();

            }


            // Block 5
            EncountHeroGetShowActor_436: ShowActors = ActorList;
            return;

            // Block 6
            EncountHeroGetShowActor_468: CallFunc_Add_IntInt_ReturnValue = Add_IntInt(Temp_int_Loop_Counter_Variable, 1);
            Temp_int_Loop_Counter_Variable = CallFunc_Add_IntInt_ReturnValue;
            goto EncountHeroGetShowActor_165;

        }


        // Block 7
        EncountHeroGetShowActor_542:

    }

    [AccessSpecifiers, FuncOverrideMatch, HasOutParms, BlueprintCallable, BlueprintEvent, FuncInherit]
    public void CheckSkeletalMeshValid(out bool isValid) {
        // Locals
        [InstancedReference] Object<SkeletalMeshComponent> CallFunc_GetSkeletalMesh_SkeletalMesh;
        bool CallFunc_IsValid_ReturnValue;

        // Block 1
        this.GetSkeletalMesh(CallFunc_GetSkeletalMesh_SkeletalMesh);
        CallFunc_IsValid_ReturnValue = (bool)(IsValid(CallFunc_GetSkeletalMesh_SkeletalMesh));
        isValid = (bool)(CallFunc_IsValid_ReturnValue);

    }

}
