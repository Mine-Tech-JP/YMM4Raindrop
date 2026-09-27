// SPDX-License-Identifier: MPL-2.0

using System.ComponentModel.DataAnnotations;
using System.Numerics;
using System.Reflection;
using System.Diagnostics;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using YMM4GlassWipe;
using YMM4GlassWipe.Verification;

Ymm4VerificationRuntime.Initialize();
if (args.Length == 2 && args[0] == "--continuous-preview")
{
    ContinuousWipeMaskVerification.WritePreviewArtifacts(args[1]);
    return 0;
}

var tests = new (string Name, Action Run)[]
{
    ("簡易物理の水量・再現性・キャッシュ", OutsideDropletPhysicsVerification.Run),
    ("簡易物理の保存・部分適用・UndoRedo", OutsideDropletPhysicsPersistenceVerification.Run),
    ("簡易物理の表示補間・転送・設定接続", OutsideDropletPhysicsRenderingVerification.Run),
    ("リアル水滴のYMM4保存とプリセット範囲", RealisticDropletVerification.VerifyPersistenceAndScopes),
    ("リアル水滴のQuad実効値とUI復帰", RealisticDropletVerification.VerifyQuadAndUi),
    ("リアル水滴の参照矩形と無効化", RealisticDropletVerification.VerifyRectangles),
    ("ユーザーブラシ失敗後の内蔵形状復旧と再試行抑制", UserBrushRecoveryVerification.VerifyBuiltInRecoveryAttempts),
    ("ユーザーブラシのID更新とGPU失敗の再試行範囲", UserBrushRecoveryVerification.VerifyUserImageRecoveryAttempts),
    ("ユーザーブラシ一覧の読込エラーと復旧案内", UserBrushSafetyVerification.VerifyEditorRecoveryGuidance),
    ("ユーザーブラシ共有PNGの参照寿命", UserBrushSafetyVerification.VerifySharedImageLifetime),
    ("ユーザーブラシ置換先のファイル名衝突保護", UserBrushSafetyVerification.VerifyReplacementNameCollision),
    ("四角形ブラシの部分回転追従と復路全体", RectangleTurnVerification.VerifyPartialFollowAcrossReturn),
    ("四角形ブラシの折り返し境界の姿勢", RectangleTurnVerification.VerifyBoundaryOrientation),
    ("四角形ブラシの連続生成と前後シーク", RectangleTurnVerification.VerifyStreamingAndSeek),
    ("中間点付き往復拭きの完了とシーク", WipeMidpointVerification.VerifySimpleCompletionAndSeek),
    ("中間点付き詳細軌跡とブラシAnimation", WipeMidpointVerification.VerifyDetailedAndBrushAnimations),
    ("拭き取り中間点の評価境界と保存値維持", WipeMidpointVerification.VerifyEvaluationBoundariesAndPreservation),
    ("ぼかし量と合成定数の更新判定を分離", VerifyBlurCompositeParameterIsolation),
    ("水滴前段の曇り専用値除外と変更検出", VerifyPreFogParameterIsolation),
    ("曇り量0の省略とデバッグ・水滴の維持", FogBypassVerification.VerifyRouting),
    ("曇り省略のAnimation評価と前後シーク", FogBypassVerification.VerifyAnimatedTransitions),
    ("マスク休止後の履歴が連続更新と一致", MaskSuspensionVerification.VerifyDeferredHistory),
    ("マスク休止後の設定変更とシークで再構築", MaskSuspensionVerification.VerifyResumeInvalidation),
    ("水滴の再形成開始と可視率の分散", OutsideDropletReformationVerification.VerifyStaggeredReformation),
    ("水滴の再形成の短時間集中を抑制", OutsideDropletReformationVerification.VerifyShortWindowConcentration),
    ("水滴の再形成境界と直接シーク", OutsideDropletReformationVerification.VerifyBoundariesAndSeek),
    ("連続拭きの選択用UIと保存名の分離", ContinuousWipeSelectionVerification.VerifySelectionSerialization),
    ("連続拭きの形状選択とプリセットのUndoRedo", ContinuousWipeSelectionVerification.VerifyUndoRedo),
    ("連続拭きの形状選択時の既定ON", ContinuousWipeSelectionVerification.VerifyShapeSelection),
    ("連続拭きの選択既定と保存復元の分離", ContinuousWipeSelectionVerification.VerifySavedValueRestoration),
    ("連続拭きの選択既定とプリセット復元の分離", ContinuousWipeSelectionVerification.VerifyPresetRestoration),
    ("連続拭きの新旧保存互換", ContinuousWipeSettingsVerification.VerifyDefaultsAndCompatibility),
    ("連続拭きのプリセットと再構築", ContinuousWipeSettingsVerification.VerifyPresetScopeAndRebuild),
    ("連続拭きの表示範囲", ContinuousWipeSettingsVerification.VerifyUiWiring),
    ("連続拭きOFFと他ブラシの互換", ContinuousWipeGeometryVerification.VerifyLegacyCompatibilityAndShapeIsolation),
    ("連続拭きの最短回転と追従", ContinuousWipeGeometryVerification.VerifyShortestAngleAndRotationFollow),
    ("連続拭きの輪郭移動上限", ContinuousWipeGeometryVerification.VerifyContinuousCornerMovement),
    ("連続拭きの折り返しと非接触", ContinuousWipeGeometryVerification.VerifyPassAndContactBoundaries),
    ("連続拭きの4096件バッチ境界", ContinuousWipeGeometryVerification.VerifyStreamingAndBatchBoundary),
    ("連続拭きの部分再構築とシーク", ContinuousWipeGeometryVerification.VerifyPartialRebuildAndRandomSeek),
    ("連続拭きの不正座標の分断", ContinuousWipeGeometryVerification.VerifyInvalidCoordinatesBreakContact),
    ("連続拭きの隙間と片道被覆", ContinuousWipeMaskVerification.VerifySweptCoverage),
    ("テンプレート95設定の新規既定値", TemplateDefaultsVerification.VerifyTemplateDefaults),
    ("旧DLL85設定の欠落値互換", TemplateDefaultsVerification.VerifyLegacyDefaults),
    ("ブラシ寸法とぼかしのEffect補完互換", CompatibilityDefaultsVerification.VerifyEffectFallbacks),
    ("ブラシ寸法のプリセット補完互換", CompatibilityDefaultsVerification.VerifyPresetFallbacks),
    ("YMM4実保存処理の値とAnimation往復", TemplateDefaultsVerification.VerifyRoundTrips),
    ("短い項目名とリセット既定値", TemplateDefaultsVerification.VerifyLabelsAndResetValues),
    ("製品名とリリース版番号", VerifyReleaseIdentity),
    ("定数バッファのサイズと16バイト境界", VerifyConstantBufferLayout),
    ("外側水滴のGPU定数ブリッジ", VerifyOutsideDropletGpuBridgeContract),
    ("外側水滴の被覆診断表示", VerifyOutsideDropletCoverageDebugViewContract),
    ("座標位相の診断表示", VerifyCoordinatePhaseDebugViewContract),
    ("領域座標差の診断表示", VerifyRegionCoordinateDeltaDebugViewContract),
    ("水滴セルHashの診断表示", VerifyOutsideDropletCellHashDebugViewContract),
    ("水滴Seedビットの診断表示", VerifyOutsideDropletSeedBitsDebugViewContract),
    ("sinなし水滴セルHashの診断表示", VerifyOutsideDropletCellHashWithoutSineDebugViewContract),
    ("静止水滴セル座標ビットの診断表示", VerifyOutsideDropletCellCoordinateBitsDebugViewContract),
    ("水滴Hash入力ビットの診断表示", VerifyOutsideDropletHashInputBitsDebugViewContract),
    ("sinなし水滴最終Hash bit 0の診断表示", VerifyOutsideDropletFinalHashBitsWithoutSineDebugViewContract),
    ("固定色returnの診断表示", VerifyFixedReturnColorDebugViewContract),
    ("sinなし水滴Mix後ビットの診断表示", VerifyOutsideDropletMixedHashBitsWithoutSineDebugViewContract),
    ("sinなし水滴合成後ビットの診断表示", VerifyOutsideDropletPreAvalancheHashBitsWithoutSineDebugViewContract),
    ("sinなし水滴乗算後ビットの診断表示", VerifyOutsideDropletWeightedHashBitsWithoutSineDebugViewContract),
    ("水滴中セル単独・Hash入力加算段階bitの診断表示", VerifyOutsideDropletMediumOnlyHashInputStagesDebugViewContract),
    ("水滴中セル単独・Hash入力bit G単独の診断表示", VerifyOutsideDropletMediumOnlyHashInputGreenBitDebugViewContract),
    ("水滴中セル単独・cell X bit G単独の診断表示", VerifyOutsideDropletMediumOnlyCellXGreenBitDebugViewContract),
    ("水滴中セル単独・直書きHash入力bit G単独の診断表示", VerifyOutsideDropletMediumOnlyInlineHashInputGreenBitDebugViewContract),
    ("水滴中セル単独・cell Y bit G単独の診断表示", VerifyOutsideDropletMediumOnlyCellYGreenBitDebugViewContract),
    ("sinなし水滴Hash合成段階の数値参照", VerifyOutsideDropletCombinationStageReference),
    ("外側水滴の時刻単独更新最適化", VerifyOutsideDropletTemporalUpdateOptimization),
    ("描画出力キャッシュA/B診断契約", VerifyCompositeOutputCacheDiagnosticContract),
    ("固定透明マスク入力A/B診断契約", VerifyCompositeMaskInputBypassDiagnosticContract),
    ("描画コールバックトレース契約", VerifyRenderCallbackTraceContract),
    ("SCENE_POSITIONの入力矩形正規化", VerifyScenePositionNormalization),
    ("Fog Amount 0のパススルー数式", VerifyFogAmountZeroPassThrough),
    ("曇り量の条件付き表示", VerifyFogSettingsVisibility),
    ("合成後のAlpha維持数式", VerifyAlphaPreservation),
    ("RegionパラメータのClamp", VerifyRegionParameterClamp),
    ("外側水滴の設定とUI互換", VerifyOutsideDropletSettingsAndUi),
    ("外側水滴の全設定プリセット限定", VerifyOutsideDropletPresetScope),
    ("外側水滴の全設定プリセット境界値往復", VerifyOutsideDropletAllPresetEdgeValueRoundTrip),
    ("外側水滴シェーダーの決定性と合成契約", VerifyOutsideDropletShaderContract),
    ("外側水滴の静止安定合成", VerifyOutsideDropletStaticStabilityReference),
    ("落下水滴型と水筋接続", VerifyFallingDropletShapeReference),
    ("外側水滴のローカル時刻とシーク決定性", VerifyOutsideDropletMotionDeterminism),
    ("外側水滴の固定エミッタ契約", VerifyOutsideDropletFixedEmitterReferenceContract),
    ("外側水滴の整数乱数契約", VerifyOutsideDropletIntegerRandomContract),
    ("雨の初回出現時刻と境界", OutsideDropletRainTimingVerification.VerifyBirthBoundariesAndDistribution),
    ("雨の待機開始とシーク再現性", OutsideDropletRainTimingVerification.VerifyWaitingAndSeekDeterminism),
    ("雨のGPU定数と描画配線", OutsideDropletRainTimingVerification.VerifyGpuLayoutAndRouting),
    ("雨の降り始めの保存互換と時刻", OutsideDropletRainSettingsVerification.VerifySettingsAndPresetCompatibility),
    ("雨の境界値とUI更新", OutsideDropletRainSettingsVerification.VerifyBoundsAndUiInvalidation),
    ("雨の合体滴の出生と待機", OutsideDropletRainMergeVerification.VerifyRainHeadBirthAndWaiting),
    ("雨の静止滴の出生と吸収因果", OutsideDropletRainMergeVerification.VerifyRainStaticBirthCausality),
    ("雨の区間境界とシーク再構築", OutsideDropletRainMergeVerification.VerifyRainEpochBoundaryAndSeek),
    ("雨の合体上限とOFF互換", OutsideDropletRainMergeVerification.VerifyRainMergeLimitsAndOffCompatibility),
    ("雨の再形成時に吸収済み小滴を復活させない", OutsideDropletRainMergeVerification.VerifyRainReformationKeepsConsumedSmallDropletsHidden),
    ("水滴の見た目のUIと保存値", OutsideDropletAppearanceVerification.VerifySettingsAndPersistence),
    ("水滴の見た目の旧プリセット互換", OutsideDropletAppearanceVerification.VerifyPresetCompatibility),
    ("水滴の見た目の変更検出", OutsideDropletAppearanceVerification.VerifyTemporalInvalidation),
    ("透明水滴の陰影と背景保持の数値参照", OutsideDropletShaderAppearanceVerification.VerifyTransparentSurface),
    ("水滴の見た目のHLSL分岐", OutsideDropletShaderAppearanceVerification.VerifyShaderRouting),
    ("重なり水滴の単独・一致輪郭保持", OutsideDropletOverlapVerification.VerifyIsolatedAndCoincident),
    ("重なり水滴の内包と外周保持", OutsideDropletOverlapVerification.VerifyContainmentAndOuterBoundary),
    ("重なり水滴の可視性遷移", OutsideDropletOverlapVerification.VerifyVisibilityTransitions),
    ("重なり水滴の評価順序と被覆保持", OutsideDropletOverlapVerification.VerifyOrderAndCoverage),
    ("重なり水滴のHLSL配線", VerifyOutsideDropletOverlapShaderContract),
    ("静止水滴セル境界の余計な線", OutsideDropletArtifactVerification.VerifyStaticCellBoundaries),
    ("水筋の先細りと滴接続の連続性", OutsideDropletArtifactVerification.VerifyTrailContinuity),
    ("水滴合体のUIと設定配線", OutsideDropletMergeSettingsVerification.VerifySourceWiring),
    ("水滴合体の保存互換とプリセット範囲", OutsideDropletMergeSettingsVerification.VerifyCodecAndPresetCompatibility),
    ("水滴合体GPU定数と配線", OutsideDropletMergeGpuVerification.VerifyGpuConstantsAndSourceWiring),
    ("水滴合体静止滴table転送", OutsideDropletMergeGpuVerification.VerifyStaticSlotTransport),
    ("水滴合体の決定性と境界", OutsideDropletMergeSimulationVerification.Run),
    ("水滴合体の連続挙動", OutsideDropletMergeBehaviorVerification.Run),
    ("静止水滴の初期吸収と決定性", OutsideDropletStaticLayoutVerification.VerifyDeterminismAndInitialAbsorption),
    ("静止水滴の多段吸収とセル境界", OutsideDropletStaticLayoutVerification.VerifyMultiStageGrowthAndCellBoundaryCap),
    ("静止水滴の列挙上限", OutsideDropletStaticLayoutVerification.VerifyBoundedFailure),
    ("小滴全体の吸収と静止大滴の成長", OutsideDropletAbsorptionVerification.VerifyWholeHeadAbsorptionAndStaticGrowth),
    ("楕円領域内の初期吸収", OutsideDropletAbsorptionVerification.VerifyInitialAbsorptionInsideEllipse),
    ("落下滴同士の小から大への吸収", OutsideDropletAbsorptionVerification.VerifyFallingHeadPairAbsorption),
    ("静止水滴のキー衝突時の独立性", OutsideDropletStaticLayoutVerification.VerifyCellKeyCollisionDoesNotInvalidateLayout),
    ("黒い輪郭の不透明度の保存互換", OutsideDropletOutlineOpacityVerification.VerifyCodecAndLegacyCompatibility),
    ("黒い輪郭の不透明度のUIと更新", OutsideDropletOutlineOpacityVerification.VerifySourceWiring),
    ("水滴込みの曇りぼかし数値参照", OutsideDropletFogRenderingVerification.VerifyFoggedDropletReference),
    ("黒輪郭の不透明度とAlpha数値参照", OutsideDropletFogRenderingVerification.VerifyOutlineOpacityAndAlphaReference),
    ("水滴前処理と最終合成のHLSL配線", OutsideDropletFogRenderingVerification.VerifyHlslRenderPassWiring),
    ("水滴を曇りの前段へ接続する条件", OutsideDropletFogPipelineVerification.VerifyRouting),
    ("Region形状enum", VerifyRegionShapeEnum),
    ("Quadプリセットenum", VerifyQuadPresetEnum),
    ("Quad Homographyの四隅一致と往復", VerifyQuadHomography),
    ("不正Quadの拒否", VerifyInvalidQuad),
    ("Quad寸法と現在領域への履歴追従", VerifyQuadSizeAndHistoryFollow),
    ("Quad境界ぼかしのピクセル換算", VerifyQuadFeather),
    ("Region回転の往復変換", VerifyRegionRotationRoundTrip),
    ("Region形状マスク", VerifyRegionShapeMask),
    ("Debug View enum", VerifyDebugViewEnum),
    ("接触率0とStroke分割", VerifyStrokeSplit),
    ("高速移動の空間再サンプリング", VerifySpatialResampling),
    ("楕円ブラシの寸法と回転", VerifyEllipseBrushGeometry),
    ("6種類のブラシ形状と埋め込みPNGマスク", VerifyBrushShapes),
    ("ブラシ寸法の入力ピクセル指定", BrushPixelAndStrokeCanvasVerification.VerifyBrushPixelDimensions),
    ("カスタム軌跡v2の1920×1080座標写像", BrushPixelAndStrokeCanvasVerification.VerifyStrokeCanvasMapping),
    ("上限付きファイル読み込み", VerifyBoundedFileReads),
    ("ユーザーブラシPNG検証とライブラリCRUD", VerifyUserBrushLibrary),
    ("ユーザーブラシ選択の保存交換", VerifyUserBrushExchange),
    ("非対称ブラシの左右反転と往復方向", VerifyAsymmetricBrushMirrorAndRoundTrip),
    ("楕円ブラシの最短回転補間", VerifyEllipseBrushRotationInterpolation),
    ("軌跡平滑化の既定値・端点・接触分割", VerifyPathSmoothing),
    ("固定Seed微小揺れの決定性", VerifyDeterministicJitter),
    ("品質別スタンプ密度と既定互換", VerifyQualitySpacingAndDefaults),
    ("定型軌跡の描画品質と自動解決", VerifySimpleGeneratedQuality),
    ("ブラシの移動方向回転追従", VerifyBrushRotationFollow),
    ("ブラシ柔らかさの保持とClamp", VerifyBrushSoftness),
    ("拭き残しと固定Seedノイズ数式", VerifyResidueAndNoiseDeterminism),
    ("重複点と0 duration", VerifyDuplicatePointAndZeroDuration),
    ("カスタム軌跡データの保存互換性", VerifyStrokeDocumentCodec),
    ("centripetal Catmull-Romの有限性", VerifyCentripetalCatmullRom),
    ("複数Strokeの分離", VerifyCustomStrokeSampling),
    ("旧方式と破損カスタム入力の選択", VerifyPathInputFallback),
    ("旧Animation経路のPathとMask互換", VerifyLegacyAnimationPathAndMaskCompatibility),
    ("かんたん作成のenumと既定値", VerifySimpleEditingModeEnumsAndDefault),
    ("かんたん作成の保存互換", VerifySimpleEditingModeCompatibility),
    ("詳細編集の軌跡種別とモード切替互換", VerifyDetailedPathInputModeCompatibility),
    ("詳細編集の内蔵プリセット", VerifyDetailedBuiltInPresets),
    ("詳細編集プリセットの種類別部分適用", VerifyDetailedPresetScopes),
    ("詳細編集プリセットの定型軌跡の注意", PresetTooltipVerification.VerifyDetailedPresetTooltip),
    ("垂れる頻度のUI・保存・プリセット範囲", FallFrequencySettingsVerification.VerifyUiPersistenceAndPresetScope),
    ("垂れる頻度の候補数・描画・再現性", FallFrequencyRenderingVerification.Run),
    ("詳細編集プリセットの保存補正", VerifyDetailedPresetCodec),
    ("ユーザープリセットのCRUDと破損保護", VerifyDetailedUserPresetStore),
    ("かんたん作成の領域写像", VerifySimpleRegionIntegration),
    ("かんたん作成のプリセット別表示", VerifySimplePresetVisibility),
    ("編集モード別のUI表示", VerifyEditingModeVisibility),
    ("かんたん作成パターンと補間", VerifySimplePathPatternsAndInterpolation),
    ("経由点あり往復のなめらかな同一軌跡", VerifyArcRoundTripSmoothRetracing),
    ("かんたん作成の進行度", VerifySimpleProgress),
    ("描画完了位置と完成状態の維持", VerifyPathCompletionAndHold),
    ("描画時間の割合・固定フレーム・秒数", VerifyPathTimingModes),
    ("始点・経由点・終点による3点円弧", VerifyThreePointCircularArcInterpolation),
    ("3点円弧の安全なフォールバックと互換性", VerifyThreePointCircularArcFallbackAndCompatibility),
    ("かんたん作成プリセット別入力適用", VerifySimplePresetInputApplicability),
    ("かんたん作成の往復回数", VerifySimpleRoundTripPatterns),
    ("0.5回単位の往復設定と保存", FractionalRoundTripVerification.VerifyUiPersistenceAndNormalization),
    ("往復スライダーの実入力範囲とモデル反映", FractionalRoundTripVerification.VerifyUiEffectiveRangeAndModelBinding),
    ("半往復の終点と片道累積", FractionalRoundTripVerification.VerifyEndpointsAndAccumulation),
    ("半往復のSnapshotとStream一致", FractionalRoundTripVerification.VerifySnapshotAndStreamConsistency),
    ("かんたん作成の1回あたりの拭き取り量", VerifySimpleWipeAmountPerPass),
    ("往復パス単位の累積数式", VerifyPerPassMaskAccumulation),
    ("往復パスのカバー率と強度分離", VerifyPerPassCoverageStrengthSeparation),
    ("往復パス端部の被覆均一性", VerifyPerPassEndpointCoverageUniformity),
    ("往復の折り返し端点を片道間で共有", VerifyRoundTripBoundaryCoverage),
    ("ずらし往復のなめらか補間", VerifyOffsetRoundTripSmoothInterpolation),
    ("ずらし往復のずらし量", VerifyOffsetRoundTripAmount),
    ("ずらし往復の画面外クリップ", VerifyOffsetRoundTripOffscreenClipping),
    ("かんたん作成の入力補正", VerifySimplePathSanitization),
    ("かんたん作成のFingerprintと更新計画", VerifySimplePathFingerprintAndUpdatePlan),
    ("Path Fingerprintと更新計画", VerifyPathFingerprintAndUpdatePlan),
    ("長尺Path Streamの固定容量列挙", VerifyLongPathStreamingBounds),
    ("Path Streamの更新計画と構造Fingerprint", VerifyStreamUpdatePlanAndDefinitionFingerprint),
    ("ストリームStampの短尺互換と固定容量", VerifyStreamingStampCompatibility),
    ("複数Strokeストリームの分離と正規順序", VerifyMergedStrokeStreaming),
    ("ランダムシーク1000回の決定性", VerifyRandomSeekDeterminism),
    ("カスタム軌跡ランダムシーク1000回", VerifyCustomRandomSeekDeterminism),
    ("シェーダー最適化分岐と埋め込み", VerifyEmbeddedShader),
};

if (args.Length == 1 && args[0] == "--maintainability-only")
{
    tests = tests.Where(test =>
        test.Run.Method.DeclaringType == typeof(CompatibilityDefaultsVerification) ||
        test.Run.Method.DeclaringType == typeof(TemplateDefaultsVerification) ||
        test.Run.Method.DeclaringType == typeof(FractionalRoundTripVerification) ||
        test.Run.Method == ((Action)VerifyOutsideDropletSettingsAndUi).Method ||
        test.Run.Method == ((Action)OutsideDropletRainSettingsVerification.VerifyBoundsAndUiInvalidation).Method).ToArray();
}
else if (args.Length == 1 && args[0] == "--brush-recovery-only")
{
    tests = tests.Where(test =>
        test.Run.Method.DeclaringType == typeof(UserBrushRecoveryVerification)).ToArray();
}
else if (args.Length == 1 && args[0] == "--continuous-only")
{
    tests = tests.Where(test => test.Name.StartsWith("連続拭き", StringComparison.Ordinal)).ToArray();
}
else if (args.Length == 1 && args[0] == "--fractional-roundtrip-only")
{
    tests = tests.Where(test =>
        test.Run.Method.DeclaringType == typeof(FractionalRoundTripVerification)).ToArray();
}
else if (args.Length == 1 && args[0] == "--fall-frequency-only")
{
    tests = tests.Where(test =>
        test.Run.Method.DeclaringType == typeof(FallFrequencySettingsVerification) ||
        test.Run.Method.DeclaringType == typeof(FallFrequencyRenderingVerification) ||
        test.Run.Method.DeclaringType == typeof(PresetTooltipVerification)).ToArray();
}

if (args.Length == 1 && args[0] == "--physics-only")
{
    tests = tests.Where(test => test.Name.StartsWith("簡易物理", StringComparison.Ordinal)).ToArray();
}

var failureCount = 0;
foreach (var test in tests)
{
    try
    {
        test.Run();
        Console.WriteLine($"[PASS] {test.Name}");
    }
    catch (Exception exception)
    {
        failureCount++;
        Console.Error.WriteLine($"[FAIL] {test.Name}: {exception.Message}");
    }
}

Console.WriteLine(
    failureCount == 0
        ? $"全{tests.Length}件の検証に合格しました。"
        : $"{failureCount}/{tests.Length}件の検証に失敗しました。");

return failureCount == 0 ? 0 : 1;

static void VerifyOutsideDropletOverlapShaderContract()
{
    var shader = ShaderVerificationSource.ReadAllText(Path.Combine(
        FindRepositoryRoot(), "src", "YMM4GlassWipe", "Shaders", "GlassComposite.hlsl"));
    var combine = RemoveWhitespace(ExtractHlslFunction(shader, "float3 CombineOutsideDropletSamples("));
    True(combine.Contains("maximumDepth=max(maximumDepth,samples[depthIndex].w);", StringComparison.Ordinal) &&
        combine.Contains("0.0f,0.06f,maximumDepth-sample.w", StringComparison.Ordinal) &&
        combine.Contains("lighting.z=max(lighting.z,sample.z);", StringComparison.Ordinal),
        "深さ差から陰影だけを抑制し、被覆を従来どおり最大値で維持する必要があります。");
    True(!combine.Contains("OutsideDropletRandom", StringComparison.Ordinal) &&
        !combine.Contains("ddx", StringComparison.Ordinal) && !combine.Contains("ddy", StringComparison.Ordinal),
        "重なり合成に乱数や不連続な形状の画面微分を追加してはいけません。");
    var gather = RemoveWhitespace(ExtractHlslFunction(shader, "float3 GetOutsideDropletLighting("));
    True(gather.Contains("float4samples[75];", StringComparison.Ordinal) &&
        gather.Contains("intsampleCount=3;", StringComparison.Ordinal) &&
        gather.Contains("sampleCount=3+headCount;", StringComparison.Ordinal) &&
        gather.Contains("returnCombineOutsideDropletSamples(samples,sampleCount);", StringComparison.Ordinal) &&
        gather.Contains("samples[3+emitterIndex]=float4(lighting,interiorDepth);", StringComparison.Ordinal) &&
        new[] { 3, 9, 15 }.All(index => gather.Contains($"samples,{index}u,refraction);", StringComparison.Ordinal)),
        "静止3件と落下最大72件の両経路を同じ合成へ渡す必要があります。");
    var staticLayer = RemoveWhitespace(ExtractHlslFunction(shader, "float3 EvaluateOutsideDropletLayer("));
    var head = RemoveWhitespace(ExtractHlslFunction(shader, "float3 RenderOutsideDropletHeadAndTrail("));
    var trail = RemoveWhitespace(ExtractHlslFunction(shader, "float3 EvaluateMappedOutsideDropletTrailSegment("));
    True(staticLayer.Contains("interiorDepth*=active*mergeVisibility;", StringComparison.Ordinal) &&
        head.Contains("interiorDepth=max(dropletDepth,trailDepth*trailFade*0.42f)*lifecycle.y*headInside;", StringComparison.Ordinal) &&
        trail.Contains("interiorDepth*=startValid*endValid;", StringComparison.Ordinal),
        "非表示の滴や無効な射影が他の輪郭を隠さないよう、可視性と水筋の弱さを深さへ反映する必要があります。");
}

static void VerifyReleaseIdentity()
{
    var repositoryRoot = FindRepositoryRoot();
    var effectSource = File.ReadAllText(
        Path.Combine(repositoryRoot, "src", "YMM4GlassWipe", "GlassWipeVideoEffect.cs"));
    True(
        effectSource.Contains(
            "internal const string DisplayName = \"雫と拭痕\";",
            StringComparison.Ordinal),
        "YMM4表示名がv1.0.0の正式名称と一致する必要があります。");
    Equal("雫と拭痕", GlassWipeVideoEffect.DisplayName, "YMM4表示名定数が正式名称と一致する必要があります。");
    Equal("雫と拭痕", new GlassWipeVideoEffect().Label, "エフェクトのLabelが正式名称と一致する必要があります。");
    var videoEffectAttributeLine = effectSource.Split('\n').Single(line =>
        line.Contains("[VideoEffect(", StringComparison.Ordinal));
    True(videoEffectAttributeLine.Contains("[\"描画\"]", StringComparison.Ordinal),
        "エフェクトのカテゴリは描画である必要があります。");
    foreach (var searchKeyword in new[]
             {
                 "雫と拭痕",
                 "シズクトフキアト",
                 "しずくとふきあと",
                 "RainDrop & GlassWipe",
                 "くもり",
                 "ホコリ",
                 "拭き取り",
                 "水滴",
                 "雨",
                 "ガラス",
             })
    {
        True(
            videoEffectAttributeLine.Contains($"\"{searchKeyword}\"", StringComparison.Ordinal),
            $"検索キーワード「{searchKeyword}」を維持する必要があります。");
    }
    foreach (var removedName in new[]
             {
                 "GlassWipe & RainDrop",
                 "GlassWipe & RainDrop FX",
                 "くもり・ホコリ拭き取り",
             })
    {
        True(
            !videoEffectAttributeLine.Contains($"\"{removedName}\"", StringComparison.Ordinal),
            $"除外対象名「{removedName}」を検索キーワードへ残してはいけません。");
    }

    var assemblyPath = Path.Combine(AppContext.BaseDirectory, "YMM4GlassWipe.dll");
    var assemblyName = AssemblyName.GetAssemblyName(assemblyPath);
    Equal(
        new Version(1, 0, 0, 0),
        assemblyName.Version,
        "互換用AssemblyVersionが1.0.0.0を維持する必要があります。");

    var versionInfo = FileVersionInfo.GetVersionInfo(assemblyPath);
    True(
        versionInfo.ProductVersion?.StartsWith("1.0.0", StringComparison.Ordinal) == true,
        "ProductVersionが1.0.0系と一致する必要があります。");
    Equal(
        "1.0.0.0",
        versionInfo.FileVersion,
        "FileVersionが1.0.0.0と一致する必要があります。");
}

static void VerifyConstantBufferLayout()
{
    var size = Marshal.SizeOf<GlassCompositeConstants>();
    Equal(240, size, "定数バッファは15レジスタ分の240バイトである必要があります。");
    Equal(0, size % 16, "定数バッファのサイズは16バイトの倍数である必要があります。");

    Equal(0, OffsetOf(nameof(GlassCompositeConstants.InputWidth)), "c0の開始位置が不正です。");
    Equal(16, OffsetOf(nameof(GlassCompositeConstants.RegionCenterX)), "c1の開始位置が不正です。");
    Equal(32, OffsetOf(nameof(GlassCompositeConstants.FogAmount)), "c2の開始位置が不正です。");
    Equal(48, OffsetOf(nameof(GlassCompositeConstants.FogTintRed)), "c3の開始位置が不正です。");
    Equal(64, OffsetOf(nameof(GlassCompositeConstants.RegionShape)), "c4の開始位置が不正です。");
    Equal(68, OffsetOf(nameof(GlassCompositeConstants.RegionRotationCos)), "c4.yの位置が不正です。");
    Equal(72, OffsetOf(nameof(GlassCompositeConstants.RegionRotationSin)), "c4.zの位置が不正です。");
    Equal(76, OffsetOf(nameof(GlassCompositeConstants.QuadValid)), "c4.wの位置が不正です。");
    Equal(80, OffsetOf(nameof(GlassCompositeConstants.WipeResidue)), "c5の開始位置が不正です。");
    Equal(84, OffsetOf(nameof(GlassCompositeConstants.WipeVariation)), "c5.yの位置が不正です。");
    Equal(88, OffsetOf(nameof(GlassCompositeConstants.FogNoise)), "c5.zの位置が不正です。");
    Equal(92, OffsetOf(nameof(GlassCompositeConstants.NoiseSeed)), "c5.wの位置が不正です。");
    Equal(96, OffsetOf(nameof(GlassCompositeConstants.QuadInverseM11)), "c6の開始位置が不正です。");
    Equal(112, OffsetOf(nameof(GlassCompositeConstants.QuadInverseM21)), "c7の開始位置が不正です。");
    Equal(128, OffsetOf(nameof(GlassCompositeConstants.QuadInverseM31)), "c8の開始位置が不正です。");
    Equal(144, OffsetOf(nameof(GlassCompositeConstants.OutsideDropletAmount)), "c9.xの位置が不正です。");
    Equal(148, OffsetOf(nameof(GlassCompositeConstants.OutsideDropletSizeScale)), "c9.yの位置が不正です。");
    Equal(152, OffsetOf(nameof(GlassCompositeConstants.OutsideDropletStrength)), "c9.zの位置が不正です。");
    Equal(156, OffsetOf(nameof(GlassCompositeConstants.OutsideDropletSeed)), "c9.wの位置が不正です。");
    Equal(160, OffsetOf(nameof(GlassCompositeConstants.OutsideDropletLocalTimeSeconds)), "c10.xの位置が不正です。");
    Equal(164, OffsetOf(nameof(GlassCompositeConstants.OutsideDropletFallEnabled)), "c10.yの位置が不正です。");
    Equal(168, OffsetOf(nameof(GlassCompositeConstants.OutsideDropletFallingRatio)), "c10.zの位置が不正です。");
    Equal(172, OffsetOf(nameof(GlassCompositeConstants.OutsideDropletFallSpeedScale)), "c10.wの位置が不正です。");
    Equal(176, OffsetOf(nameof(GlassCompositeConstants.OutsideDropletTrailLength)), "c11.xの位置が不正です。");
    Equal(180, OffsetOf(nameof(GlassCompositeConstants.OutsideDropletDeformWithSurface)), "c11.yの位置が不正です。");
    Equal(184, OffsetOf(nameof(GlassCompositeConstants.OutsideDropletAppearance)), "c11.zの位置が不正です。");
    Equal(192, OffsetOf(nameof(GlassCompositeConstants.QuadForwardM11)), "c12の開始位置が不正です。");
    Equal(208, OffsetOf(nameof(GlassCompositeConstants.QuadForwardM21)), "c13の開始位置が不正です。");
    Equal(224, OffsetOf(nameof(GlassCompositeConstants.QuadForwardM31)), "c14の開始位置が不正です。");
}

static void VerifyOutsideDropletGpuBridgeContract()
{
    var repositoryRoot = FindRepositoryRoot();
    var effectSource = RemoveWhitespace(File.ReadAllText(
        Path.Combine(
            repositoryRoot,
            "src",
            "YMM4GlassWipe",
            "GlassCompositeCustomEffect.cs")));
    var parametersSource = RemoveWhitespace(File.ReadAllText(
        Path.Combine(
            repositoryRoot,
            "src",
            "YMM4GlassWipe",
            "GlassWipeParameters.cs")));

    var dropletProperties = new (string Name, int Index, string ParameterExpression)[]
    {
        ("OutsideDropletAmount", 32, "parameters.OutsideDropletAmount"),
        ("OutsideDropletSizeScale", 33, "parameters.OutsideDropletSizeScale"),
        ("OutsideDropletStrength", 34, "parameters.OutsideDropletStrength"),
        ("OutsideDropletSeed", 35, "parameters.OutsideDropletSeed"),
        ("OutsideDropletLocalTimeSeconds", 36, "parameters.OutsideDropletLocalTimeSeconds"),
        ("OutsideDropletFallEnabled", 37, "parameters.OutsideDropletFallEnabled"),
        ("OutsideDropletFallingRatio", 38, "parameters.OutsideDropletFallingRatio"),
        ("OutsideDropletFallSpeedScale", 39, "parameters.OutsideDropletFallSpeedScale"),
        ("OutsideDropletTrailLength", 40, "parameters.OutsideDropletTrailLength"),
        ("OutsideDropletDeformWithSurface", 41, "parameters.OutsideDropletDeformWithSurface"),
        ("OutsideDropletAppearance", 51, "parameters.OutsideDropletAppearance"),
    };
    var forwardProperties = new (string Name, int Index, string ParameterExpression)[]
    {
        ("QuadForwardM11", 42, "parameters.QuadMapping.LocalToInput.M11"),
        ("QuadForwardM12", 43, "parameters.QuadMapping.LocalToInput.M12"),
        ("QuadForwardM13", 44, "parameters.QuadMapping.LocalToInput.M13"),
        ("QuadForwardM21", 45, "parameters.QuadMapping.LocalToInput.M21"),
        ("QuadForwardM22", 46, "parameters.QuadMapping.LocalToInput.M22"),
        ("QuadForwardM23", 47, "parameters.QuadMapping.LocalToInput.M23"),
        ("QuadForwardM31", 48, "parameters.QuadMapping.LocalToInput.M31"),
        ("QuadForwardM32", 49, "parameters.QuadMapping.LocalToInput.M32"),
        ("QuadForwardM33", 50, "parameters.QuadMapping.LocalToInput.M33"),
    };

    const string enumMarker = "internalenumProperties{";
    var enumStart = effectSource.IndexOf(enumMarker, StringComparison.Ordinal);
    var enumEnd = effectSource.IndexOf('}', enumStart + enumMarker.Length);
    True(enumStart >= 0 && enumEnd > enumStart, "EffectImpl.Properties enumを解析できません。");
    var propertyNames = effectSource[
            (enumStart + enumMarker.Length)..enumEnd]
        .Replace("InputWidth=0", "InputWidth", StringComparison.Ordinal)
        .Split(',', StringSplitOptions.RemoveEmptyEntries);

    foreach (var propertyContract in dropletProperties.Concat(forwardProperties))
    {
        Equal(
            propertyContract.Index,
            Array.IndexOf(propertyNames, propertyContract.Name),
            $"{propertyContract.Name}のEffect property番号");

        True(
            effectSource.Contains(
                $"SetValue((int)EffectImpl.Properties.{propertyContract.Name},{propertyContract.ParameterExpression});",
                StringComparison.Ordinal),
            $"ApplyParametersから{propertyContract.Name}をGPU propertyへ渡す必要があります。");
        var setter = propertyContract.Name == "OutsideDropletLocalTimeSeconds"
            ? "SetOutsideDropletLocalTimeSeconds(value)"
            : $"SetConstant(ref_constantBuffer.{propertyContract.Name},value)";
        True(
            effectSource.Contains(
                $"[CustomEffectProperty(PropertyType.Float,(int)Properties.{propertyContract.Name})]publicfloat{propertyContract.Name}{{get=>_constantBuffer.{propertyContract.Name};set=>{setter};}}",
                StringComparison.Ordinal),
            $"{propertyContract.Name}を同番号・同名の定数バッファfieldへ渡す必要があります。");
        if (propertyContract.Name == "OutsideDropletLocalTimeSeconds")
        {
            True(effectSource.Contains(
                "_constantBuffer.OutsideDropletLocalTimeSeconds=value;RebuildMergeConstants();UploadConstants();",
                StringComparison.Ordinal),
                "時刻setterはcoreを更新し、合体評価後に定数を送信する必要があります。");
        }
    }

    foreach (var requiredParameterExpression in new[]
             {
                 "OutsideDropletSettings.SanitizeAmount(item.OutsideDropletAmount)",
                 "OutsideDropletSettings.SanitizeSizeScale(item.OutsideDropletSize)",
                 "OutsideDropletSettings.SanitizeStrength(item.OutsideDropletStrength)",
                 "OutsideDropletSettings.SanitizeSeed(item.OutsideDropletSeed)",
                 "ResolveOutsideDropletLocalTimeSeconds(item.OutsideDropletFallEnabled,item.OutsideDropletRainEnabled,frame,fps)",
                 "item.OutsideDropletFallEnabled?1:0",
                 "OutsideDropletSettings.SanitizeFallingRatio(item.OutsideDropletFallingRatio)",
                 "OutsideDropletSettings.SanitizeFallSpeedScale(item.OutsideDropletFallSpeed)",
                 "OutsideDropletSettings.SanitizeTrailLength(item.OutsideDropletTrailLength)",
                 "OutsideDropletSettings.ResolveDeformWithSurface(item.OutsideDropletAppearance,item.OutsideDropletDeformWithSurface)?1:0",
             })
    {
        True(
            parametersSource.Contains(requiredParameterExpression, StringComparison.Ordinal),
            $"GlassWipeParameters.Createの受け渡しが不足しています: {requiredParameterExpression}");
    }
}

static void VerifyOutsideDropletCoverageDebugViewContract()
{
    var repositoryRoot = FindRepositoryRoot();
    var shaderSource = RemoveWhitespace(ShaderVerificationSource.ReadAllText(
        Path.Combine(
            repositoryRoot,
            "src",
            "YMM4GlassWipe",
            "Shaders",
            "GlassComposite.hlsl")));
    var resourcesSource = RemoveWhitespace(File.ReadAllText(
        Path.Combine(
            repositoryRoot,
            "src",
            "YMM4GlassWipe",
            "GlassWipeEffectResources.cs")));

    True(
        shaderSource.Contains(
            "if(debugView>=5.5f&&debugView<6.5f)",
            StringComparison.Ordinal),
        "外側水滴の被覆診断表示は専用のDebug View値に限定する必要があります。");
    True(
        shaderSource.Contains(
            "coverage=saturate(dropletLighting.z*regionMask*outsideDropletStrength);",
            StringComparison.Ordinal),
        "被覆診断表示は入力色ではなく外側水滴の被覆率を使用する必要があります。");
    True(
        shaderSource.Contains(
            "returnfloat4(coverage,coverage,coverage,1.0f);",
            StringComparison.Ordinal),
        "被覆診断表示は入力Alphaに依存しない白黒画像を返す必要があります。");
    var coverageStart = shaderSource.IndexOf(
        "floatcoverage=0.0f;",
        StringComparison.Ordinal);
    var wipeSampleStart = shaderSource.IndexOf(
        "floatsampledWipeMask=0.0f;",
        coverageStart,
        StringComparison.Ordinal);
    True(
        coverageStart >= 0 && wipeSampleStart > coverageStart,
        "被覆診断表示は拭き取りマスクの読み取りより前に完結する必要があります。");
    const string coverageReturn = "returnfloat4(coverage,coverage,coverage,1.0f);";
    var coverageReturnStart = shaderSource.IndexOf(coverageReturn, coverageStart, StringComparison.Ordinal);
    True(coverageReturnStart > coverageStart && coverageReturnStart < wipeSampleStart,
        "被覆率だけを返して診断ブロックを終了する必要があります。");
    var coverageBlock = shaderSource[coverageStart..(coverageReturnStart + coverageReturn.Length)];
    True(
        !coverageBlock.Contains("OriginalTexture", StringComparison.Ordinal) &&
        !coverageBlock.Contains("BlurredTexture", StringComparison.Ordinal) &&
        !coverageBlock.Contains("MaskTexture", StringComparison.Ordinal) &&
        !coverageBlock.Contains("sampledWipeMask", StringComparison.Ordinal) &&
        !coverageBlock.Contains("effectiveWipeMask", StringComparison.Ordinal),
        "被覆診断表示の返却値を入力色、ぼかし、拭き取りマスクへ依存させてはいけません。");
    True(
        !resourcesSource.Contains(
            "InvalidateEffectInputRectangle",
            StringComparison.Ordinal),
        "不合格となった入力全域無効化A/Bを残してはいけません。");

#if DEBUG
    Equal(
        6,
        (int)GlassWipeDebugView.OutsideDropletCoverage,
        "Debug版の外側水滴被覆表示は値6である必要があります。");
#else
    True(
        !Enum.IsDefined(typeof(GlassWipeDebugView), 6),
        "Release版へ診断専用のDebug View値を公開してはいけません。");
    Equal(
        GlassWipeDebugView.Final,
        GlassWipeParameterSanitizer.SanitizeDebugView((GlassWipeDebugView)6),
        "Release版は診断専用値を最終結果へ補正する必要があります。");
#endif
}

static void VerifyCoordinatePhaseDebugViewContract()
{
    var shaderSource = RemoveWhitespace(ShaderVerificationSource.ReadAllText(
        Path.Combine(
            FindRepositoryRoot(),
            "src",
            "YMM4GlassWipe",
            "Shaders",
            "GlassComposite.hlsl")));

    var diagnosticStart = shaderSource.IndexOf(
        "if(debugView>=6.5f&&debugView<7.5f)",
        StringComparison.Ordinal);
    var originalSampleStart = shaderSource.IndexOf(
        "float4original=OriginalTexture.Sample",
        StringComparison.Ordinal);
    True(
        diagnosticStart >= 0 && originalSampleStart > diagnosticStart,
        "座標位相診断は元画像のサンプリングより前に完結する必要があります。");
    var diagnosticBlock = shaderSource[diagnosticStart..originalSampleStart];
    True(
        diagnosticBlock.Contains(
            "float3diagnosticPhase=float3(GetDiagnosticCoordinatePhase(position.xy),GetDiagnosticCoordinatePhase(inputPixelPosition),GetDiagnosticCoordinatePhase(originalUv.xy*safeInputSize-inputPixelPosition));",
            StringComparison.Ordinal),
        "座標位相診断はSV、SCENE由来の入力座標、元画像UVとの差をRGBで表示する必要があります。");
    True(
        diagnosticBlock.Contains(
            "returnfloat4(0.20f+diagnosticPhase*0.80f,1.0f);",
            StringComparison.Ordinal),
        "座標位相診断は入力Alphaに依存しない色分け表示を返す必要があります。");
    True(
        !diagnosticBlock.Contains("Texture", StringComparison.Ordinal) &&
        !diagnosticBlock.Contains("Sample", StringComparison.Ordinal) &&
        !diagnosticBlock.Contains("GetOutsideDropletLighting", StringComparison.Ordinal) &&
        !diagnosticBlock.Contains("originalUv.x>=", StringComparison.Ordinal),
        "座標位相診断を画像サンプリングや水滴生成へ依存させてはいけません。");

    var phaseFunction = ExtractHlslFunction(
        shaderSource,
        "floatGetDiagnosticCoordinatePhase(");
    True(
        phaseFunction.Contains("frac(", StringComparison.Ordinal) &&
        phaseFunction.Contains("/17.0f", StringComparison.Ordinal) &&
        phaseFunction.Contains("/29.0f", StringComparison.Ordinal) &&
        !phaseFunction.Contains("fwidth", StringComparison.Ordinal) &&
        !phaseFunction.Contains("Hash", StringComparison.Ordinal),
        "座標位相は導関数や水滴Hashを使わず、異なる二周期から決定的に算出する必要があります。");

#if DEBUG
    Equal(
        7,
        (int)GlassWipeDebugView.CoordinatePhase,
        "Debug版の座標位相表示は値7である必要があります。");
#else
    True(
        !Enum.IsDefined(typeof(GlassWipeDebugView), 7),
        "Release版へ座標位相の診断用Debug View値を公開してはいけません。");
    Equal(
        GlassWipeDebugView.Final,
        GlassWipeParameterSanitizer.SanitizeDebugView((GlassWipeDebugView)7),
        "Release版は座標位相の診断用値を最終結果へ補正する必要があります。");
#endif
}

static void VerifyRegionCoordinateDeltaDebugViewContract()
{
    var shaderSource = RemoveWhitespace(ShaderVerificationSource.ReadAllText(
        Path.Combine(
            FindRepositoryRoot(),
            "src",
            "YMM4GlassWipe",
            "Shaders",
            "GlassComposite.hlsl")));

    var diagnosticStart = shaderSource.IndexOf(
        "if(debugView>=7.5f&&debugView<8.5f)",
        StringComparison.Ordinal);
    var cellHashStart = shaderSource.IndexOf(
        "if(debugView>=8.5f&&debugView<9.5f)",
        diagnosticStart,
        StringComparison.Ordinal);
    True(
        diagnosticStart >= 0 && cellHashStart > diagnosticStart,
        "領域座標差診断は領域座標の算出後、水滴セルHash診断より前に完結する必要があります。");
    var diagnosticBlock = shaderSource[diagnosticStart..cellHashStart];
    True(
        diagnosticBlock.Contains(
            "float2regionPixelDelta=regionUv*diagnosticRegionPixelSize-inputPixelPosition;",
            StringComparison.Ordinal),
        "領域座標差診断は水滴用領域座標と入力座標の差を直接使用する必要があります。");
    True(
        diagnosticBlock.Contains(
            "constfloatdiagnosticAmplification=512.0f;",
            StringComparison.Ordinal) &&
        diagnosticBlock.Contains(
            "returnfloat4(signedDelta,saturate(deltaMagnitude*2.0f),1.0f);",
            StringComparison.Ordinal),
        "領域座標差診断は微小差を512倍し、入力Alphaに依存しない表示を返す必要があります。");
    True(
        diagnosticBlock.Contains(
            "if(deltaMagnitude>=0.49f){returnfloat4(1.0f,0.0f,1.0f,1.0f);}",
            StringComparison.Ordinal) &&
        diagnosticBlock.Contains(
            "if(any(regionPixelDelta!=regionPixelDelta)){returnfloat4(1.0f,1.0f,0.0f,1.0f);}",
            StringComparison.Ordinal),
        "領域座標差診断は測定範囲外をマゼンタ、無効値を黄色で明示する必要があります。");
    True(
        !diagnosticBlock.Contains("GetOutsideDropletLighting", StringComparison.Ordinal) &&
        !diagnosticBlock.Contains("Hash", StringComparison.Ordinal) &&
        !diagnosticBlock.Contains("fwidth", StringComparison.Ordinal) &&
        !diagnosticBlock.Contains("Texture.Sample", StringComparison.Ordinal),
        "領域座標差診断の計算へ水滴生成、Hash、導関数、追加の画像サンプリングを含めてはいけません。");

#if DEBUG
    Equal(
        8,
        (int)GlassWipeDebugView.RegionCoordinateDelta,
        "Debug版の領域座標差表示は値8である必要があります。");
#else
    True(
        !Enum.IsDefined(typeof(GlassWipeDebugView), 8),
        "Release版へ領域座標差の診断用Debug View値を公開してはいけません。");
    Equal(
        GlassWipeDebugView.Final,
        GlassWipeParameterSanitizer.SanitizeDebugView((GlassWipeDebugView)8),
        "Release版は領域座標差の診断用値を最終結果へ補正する必要があります。");
#endif
}

static void VerifyOutsideDropletCellHashDebugViewContract()
{
    var shaderSource = RemoveWhitespace(ShaderVerificationSource.ReadAllText(
        Path.Combine(
            FindRepositoryRoot(),
            "src",
            "YMM4GlassWipe",
            "Shaders",
            "GlassComposite.hlsl")));

    var fingerprintFunction = ExtractHlslFunction(
        shaderSource,
        "floatGetOutsideDropletCellStateFingerprint(");
    foreach (var requiredHash in new[]
             {
                 "HashNoise2D(cell,seed+3.1f)",
                 "HashNoise2D(cell,seed+11.7f)",
                 "HashNoise2D(cell,seed+29.3f)",
                 "HashNoise2D(cell,seed+47.9f)",
                 "HashNoise2D(cell,seed+61.1f)",
             })
    {
        True(
            fingerprintFunction.Contains(requiredHash, StringComparison.Ordinal),
            $"水滴セルHash fingerprintに実描画のHashが不足しています: {requiredHash}");
    }
    True(
        fingerprintFunction.Contains(
            "returnsaturate(activationHash*0.31f+centerXHash*0.23f+centerYHash*0.19f+radiusHash*0.15f+verticalScaleHash*0.12f);",
            StringComparison.Ordinal),
        "水滴セルHash fingerprintは全Hashを固定係数で決定的に集約する必要があります。");
    True(
        !fingerprintFunction.Contains("localPosition", StringComparison.Ordinal) &&
        !fingerprintFunction.Contains("step(", StringComparison.Ordinal) &&
        !fingerprintFunction.Contains("EvaluateOutsideDropletAppearance", StringComparison.Ordinal) &&
        !fingerprintFunction.Contains("fwidth", StringComparison.Ordinal),
        "水滴セルHash fingerprintへセル内位置、出現判定、形状、導関数を混入させてはいけません。");

    var diagnosticStart = shaderSource.IndexOf(
        "if(debugView>=8.5f&&debugView<9.5f)",
        StringComparison.Ordinal);
    var coverageStart = shaderSource.IndexOf(
        "if(debugView>=5.5f&&debugView<6.5f)",
        diagnosticStart,
        StringComparison.Ordinal);
    True(
        diagnosticStart >= 0 && coverageStart > diagnosticStart,
        "水滴セルHash診断は外側水滴の被覆診断より前に完結する必要があります。");
    var diagnosticBlock = shaderSource[diagnosticStart..coverageStart];
    foreach (var requiredLayer in new[]
             {
                 "GetOutsideDropletCellStateFingerprint(staticPatternPosition,20.0f*sizeScale,outsideDropletSeed+101.0f)",
                 "GetOutsideDropletCellStateFingerprint(staticPatternPosition,44.0f*sizeScale,outsideDropletSeed+307.0f)",
                 "GetOutsideDropletCellStateFingerprint(staticPatternPosition,92.0f*sizeScale,outsideDropletSeed+701.0f)",
             })
    {
        True(
            diagnosticBlock.Contains(requiredLayer, StringComparison.Ordinal),
            $"水滴セルHash診断の層が不足しています: {requiredLayer}");
    }
    True(
        diagnosticBlock.Contains(
            "returnfloat4(cellStateFingerprint,1.0f);",
            StringComparison.Ordinal),
        "水滴セルHash診断は小・中・大のfingerprintをRGBで返す必要があります。");
    True(
        !diagnosticBlock.Contains("GetOutsideDropletLighting", StringComparison.Ordinal) &&
        !diagnosticBlock.Contains("EvaluateOutsideDropletAppearance", StringComparison.Ordinal) &&
        !diagnosticBlock.Contains("fwidth", StringComparison.Ordinal) &&
        !diagnosticBlock.Contains("Texture.Sample", StringComparison.Ordinal),
        "水滴セルHash診断の計算へ被覆、形状、導関数、追加の画像サンプリングを含めてはいけません。");

#if DEBUG
    Equal(
        9,
        (int)GlassWipeDebugView.OutsideDropletCellHash,
        "Debug版の水滴セルHash表示は値9である必要があります。");
#else
    True(
        !Enum.IsDefined(typeof(GlassWipeDebugView), 9),
        "Release版へ水滴セルHashの診断用Debug View値を公開してはいけません。");
    Equal(
        GlassWipeDebugView.Final,
        GlassWipeParameterSanitizer.SanitizeDebugView((GlassWipeDebugView)9),
        "Release版は水滴セルHashの診断用値を最終結果へ補正する必要があります。");
#endif
}

static void VerifyOutsideDropletSeedBitsDebugViewContract()
{
    var shaderSource = RemoveWhitespace(ShaderVerificationSource.ReadAllText(
        Path.Combine(
            FindRepositoryRoot(),
            "src",
            "YMM4GlassWipe",
            "Shaders",
            "GlassComposite.hlsl")));

    var diagnosticStart = shaderSource.IndexOf(
        "if(debugView>=9.5f&&debugView<10.5f)",
        StringComparison.Ordinal);
    True(
        diagnosticStart >= 0,
        "水滴Seedビット診断の分岐が見つかりません。");
    var cellHashStart = shaderSource.IndexOf(
        "if(debugView>=8.5f&&debugView<9.5f)",
        Math.Max(diagnosticStart, 0),
        StringComparison.Ordinal);
    True(
        cellHashStart > diagnosticStart,
        "水滴Seedビット診断は静止水滴セルHash診断より前に完結する必要があります。");
    var diagnosticBlock = shaderSource[diagnosticStart..cellHashStart];
    foreach (var requiredExpression in new[]
             {
                 "uintseedBits=asuint(outsideDropletSeed);",
                 "uintstripeIndex=(uint)floor(max(scenePosition.x,0.0f)/8.0f);",
                 "uintbitIndex=stripeIndex&31u;",
                 "floatbitValue=(float)((seedBits>>bitIndex)&1u);",
                 "returnfloat4(bitValue,bitValue,bitValue,1.0f);",
             })
    {
        True(
            diagnosticBlock.Contains(requiredExpression, StringComparison.Ordinal),
            $"水滴Seedビット診断の32ビット縦縞契約が不足しています: {requiredExpression}");
    }

    True(
        !diagnosticBlock.Contains("HashNoise2D", StringComparison.Ordinal) &&
        !diagnosticBlock.Contains("GetOutsideDropletCellStateFingerprint", StringComparison.Ordinal) &&
        !diagnosticBlock.Contains("sin(", StringComparison.Ordinal) &&
        !diagnosticBlock.Contains("fwidth", StringComparison.Ordinal) &&
        !diagnosticBlock.Contains("Texture.Sample", StringComparison.Ordinal) &&
        !diagnosticBlock.Contains("outsideDropletLocalTimeSeconds", StringComparison.Ordinal) &&
        !diagnosticBlock.Contains("inputUv", StringComparison.Ordinal) &&
        !diagnosticBlock.Contains("inputOrigin", StringComparison.Ordinal) &&
        !diagnosticBlock.Contains("safeInputSize", StringComparison.Ordinal),
        "水滴Seedビット診断へHash、セル、導関数、画像サンプリング、落下時刻、入力ROI座標を含めてはいけません。");

#if DEBUG
    Equal(
        10,
        (int)GlassWipeDebugView.OutsideDropletSeedBits,
        "Debug版の水滴Seedビット表示は値10である必要があります。");
    Equal(
        "水滴Seedビット（診断）",
        GetDisplayName(GlassWipeDebugView.OutsideDropletSeedBits),
        "水滴Seedビット診断の表示名が不正です。");
#else
    True(
        !Enum.IsDefined(typeof(GlassWipeDebugView), 10),
        "Release版へ水滴Seedビットの診断用Debug View値を公開してはいけません。");
    Equal(
        GlassWipeDebugView.Final,
        GlassWipeParameterSanitizer.SanitizeDebugView((GlassWipeDebugView)10),
        "Release版は水滴Seedビットの診断用値を最終結果へ補正する必要があります。");
#endif
}

static void VerifyOutsideDropletCellHashWithoutSineDebugViewContract()
{
    var shaderSource = RemoveWhitespace(ShaderVerificationSource.ReadAllText(
        Path.Combine(
            FindRepositoryRoot(),
            "src",
            "YMM4GlassWipe",
            "Shaders",
            "GlassComposite.hlsl")));

    var hashInputFunction = ExtractHlslFunction(
        shaderSource,
        "floatGetOutsideDropletHashInputWithoutSine(");
    True(
        hashInputFunction.Contains(
            "returndot(lattice,float2(127.1f,311.7f))+(seed+offset)*74.7f;",
            StringComparison.Ordinal),
        "sinなしHashの入力式は診断表示と共有する必要があります。");

    var mixFunction = ExtractHlslFunction(
        shaderSource,
        "uintMixOutsideDropletHashInputWithoutSine(");
    True(
        mixFunction.Contains(
            "floathashInput=GetOutsideDropletHashInputWithoutSine(lattice,seed,offset);",
            StringComparison.Ordinal) &&
        mixFunction.Contains("uintmixed=asuint(hashInput);", StringComparison.Ordinal) &&
        mixFunction.Contains("mixed^=mixed>>16;", StringComparison.Ordinal) &&
        mixFunction.Contains("mixed*=0x7feb352du;", StringComparison.Ordinal) &&
        mixFunction.Contains("mixed^=mixed>>15;", StringComparison.Ordinal) &&
        mixFunction.Contains("mixed*=0x846ca68bu;", StringComparison.Ordinal),
        "sinなしHashは本番と同じhashInputを32ビット整数ミックスへ渡す必要があります。");
    True(
        !mixFunction.Contains("sin(", StringComparison.Ordinal) &&
        !mixFunction.Contains("frac(", StringComparison.Ordinal),
        "sinなしHashへsinまたはfracを含めてはいけません。");

    var fingerprintBitsFunction = ExtractHlslFunction(
        shaderSource,
        "uintGetOutsideDropletCellStateFingerprintBitsWithoutSine(");
    var fingerprintFunction = ExtractHlslFunction(
        shaderSource,
        "floatGetOutsideDropletCellStateFingerprintWithoutSine(");
    True(
        fingerprintBitsFunction.Contains(
            "float2cell=floor(patternPosition/safeCellSize);",
            StringComparison.Ordinal),
        "sinなしHashは診断9と同じセル計算を使う必要があります。");
    foreach (var requiredHashInput in new[]
             {
                 "MixOutsideDropletHashInputWithoutSine(cell,seed,3.1f)",
                 "MixOutsideDropletHashInputWithoutSine(cell,seed,11.7f)",
                 "MixOutsideDropletHashInputWithoutSine(cell,seed,29.3f)",
                 "MixOutsideDropletHashInputWithoutSine(cell,seed,47.9f)",
                 "MixOutsideDropletHashInputWithoutSine(cell,seed,61.1f)",
             })
    {
        True(
            fingerprintBitsFunction.Contains(requiredHashInput, StringComparison.Ordinal),
            $"sinなしHashに実描画相当のHash入力が不足しています: {requiredHashInput}");
    }

    True(
        fingerprintBitsFunction.Contains(
            "uintfolded=fingerprint^(fingerprint>>8)^(fingerprint>>16)^(fingerprint>>24);",
            StringComparison.Ordinal) &&
        fingerprintBitsFunction.Contains(
            "returnfolded;",
            StringComparison.Ordinal) &&
        fingerprintFunction.Contains(
            "uintfolded=GetOutsideDropletCellStateFingerprintBitsWithoutSine(patternPosition,cellSizePixels,seed);",
            StringComparison.Ordinal) &&
        fingerprintFunction.Contains(
            "return(float)(folded&255u)/255.0f;",
            StringComparison.Ordinal),
        "sinなしHashは最終32ビットを共有し、Debug11では低位byteを表示する必要があります。");
    True(
        !fingerprintBitsFunction.Contains("HashNoise2D", StringComparison.Ordinal) &&
        !fingerprintBitsFunction.Contains("sin(", StringComparison.Ordinal) &&
        !fingerprintBitsFunction.Contains("frac(", StringComparison.Ordinal) &&
        !fingerprintFunction.Contains("sin(", StringComparison.Ordinal) &&
        !fingerprintFunction.Contains("frac(", StringComparison.Ordinal),
        "sinなしHashへ本番のsin系Hashを混入させてはいけません。");

    var diagnosticGroupStart = shaderSource.IndexOf(
        "if(debugView>=10.5f&&debugView<23.5f)",
        StringComparison.Ordinal);
    var diagnosticStart = shaderSource.IndexOf(
        "if(debugView<11.5f)",
        Math.Max(diagnosticGroupStart, 0),
        StringComparison.Ordinal);
    True(
        diagnosticGroupStart >= 0 && diagnosticStart > diagnosticGroupStart,
        "sinなし水滴セルHash診断の分岐が見つかりません。");
    var coordinateBitsStart = shaderSource.IndexOf(
        "if(debugView<12.5f)",
        Math.Max(diagnosticStart, 0),
        StringComparison.Ordinal);
    True(
        coordinateBitsStart > diagnosticStart,
        "sinなし水滴セルHash診断はセル座標ビット診断より前に完結する必要があります。");
    var diagnosticBlock = shaderSource[diagnosticStart..coordinateBitsStart];
    foreach (var requiredLayer in new[]
             {
                 "GetOutsideDropletCellStateFingerprintWithoutSine(staticPatternPosition,20.0f*sizeScale,outsideDropletSeed+101.0f)",
                 "GetOutsideDropletCellStateFingerprintWithoutSine(staticPatternPosition,44.0f*sizeScale,outsideDropletSeed+307.0f)",
                 "GetOutsideDropletCellStateFingerprintWithoutSine(staticPatternPosition,92.0f*sizeScale,outsideDropletSeed+701.0f)",
             })
    {
        True(
            diagnosticBlock.Contains(requiredLayer, StringComparison.Ordinal),
            $"sinなし水滴セルHash診断の層が不足しています: {requiredLayer}");
    }

    True(
        diagnosticBlock.Contains(
            "returnfloat4(noSineCellState,1.0f);",
            StringComparison.Ordinal),
        "sinなし水滴セルHash診断は3層をRGB、Alpha 1で返す必要があります。");
    var diagnosticReturnStart = shaderSource.IndexOf(
        "returnfloat4(noSineCellState,1.0f);",
        Math.Max(diagnosticStart, 0),
        StringComparison.Ordinal);
    var originalSampleStart = shaderSource.IndexOf(
        "OriginalTexture.Sample(",
        StringComparison.Ordinal);
    var blurredSampleStart = shaderSource.IndexOf(
        "BlurredTexture.Sample(",
        StringComparison.Ordinal);
    var maskSampleStart = shaderSource.IndexOf(
        "MaskTexture.SampleLevel(",
        StringComparison.Ordinal);
    True(
        diagnosticReturnStart > diagnosticStart &&
        originalSampleStart > diagnosticReturnStart &&
        blurredSampleStart > diagnosticReturnStart &&
        maskSampleStart > diagnosticReturnStart,
        "sinなし水滴セルHash診断はOriginal、Blurred、Maskの全サンプリングより前に完結する必要があります。");
    True(
        !diagnosticBlock.Contains("HashNoise2D", StringComparison.Ordinal) &&
        !diagnosticBlock.Contains("sin(", StringComparison.Ordinal) &&
        !diagnosticBlock.Contains("fwidth", StringComparison.Ordinal) &&
        !diagnosticBlock.Contains("Texture.Sample", StringComparison.Ordinal) &&
        !diagnosticBlock.Contains("GetOutsideDropletLighting", StringComparison.Ordinal),
        "sinなし水滴セルHash診断へsin系Hash、導関数、画像サンプリング、照明を含めてはいけません。");

#if DEBUG
    Equal(
        11,
        (int)GlassWipeDebugView.OutsideDropletCellHashWithoutSine,
        "Debug版のsinなし水滴セルHash表示は値11である必要があります。");
    Equal(
        "静止セルHash（sinなし診断）",
        GetDisplayName(GlassWipeDebugView.OutsideDropletCellHashWithoutSine),
        "sinなし水滴セルHash診断の表示名が不正です。");
#else
    True(
        !Enum.IsDefined(typeof(GlassWipeDebugView), 11),
        "Release版へsinなし水滴セルHashの診断用Debug View値を公開してはいけません。");
    Equal(
        GlassWipeDebugView.Final,
        GlassWipeParameterSanitizer.SanitizeDebugView((GlassWipeDebugView)11),
        "Release版はsinなし水滴セルHashの診断用値を最終結果へ補正する必要があります。");
#endif
}

static void VerifyOutsideDropletCellCoordinateBitsDebugViewContract()
{
    var shaderSource = RemoveWhitespace(ShaderVerificationSource.ReadAllText(
        Path.Combine(
            FindRepositoryRoot(),
            "src",
            "YMM4GlassWipe",
            "Shaders",
            "GlassComposite.hlsl")));

    var diagnosticStart = shaderSource.IndexOf(
        "if(debugView>=10.5f&&debugView<23.5f)",
        StringComparison.Ordinal);
    True(
        diagnosticStart >= 0,
        "静止水滴セル座標ビット診断の分岐が見つかりません。");
    var mediumOnlyBranchStart = shaderSource.IndexOf(
        "if(debugView>=18.5f&&debugView<19.5f)",
        Math.Max(diagnosticStart, 0),
        StringComparison.Ordinal);
    True(
        mediumOnlyBranchStart > diagnosticStart,
        "中セル単独診断より前の共有座標計算を分離できません。");
    var coordinateBranchStart = shaderSource.IndexOf(
        "if(debugView<12.5f)",
        Math.Max(diagnosticStart, 0),
        StringComparison.Ordinal);
    True(
        coordinateBranchStart > diagnosticStart,
        "静止水滴セル座標ビット診断の専用分岐が見つかりません。");
    var hashInputBranchStart = shaderSource.IndexOf(
        "if(debugView<13.5f)",
        Math.Max(coordinateBranchStart, 0),
        StringComparison.Ordinal);
    True(
        hashInputBranchStart > coordinateBranchStart,
        "静止水滴セル座標ビット診断はHash入力ビット診断より前に完結する必要があります。");
    var noSineBranchStart = shaderSource.IndexOf(
        "if(debugView<11.5f)",
        Math.Max(diagnosticStart, 0),
        StringComparison.Ordinal);
    var cellSetupStart = shaderSource.IndexOf(
        "float2smallCell=floor(",
        Math.Max(noSineBranchStart, 0),
        StringComparison.Ordinal);
    True(
        noSineBranchStart > mediumOnlyBranchStart &&
        cellSetupStart > noSineBranchStart &&
        cellSetupStart < coordinateBranchStart,
        "静止水滴セル座標ビット診断の共有計算をDebug 11から分離できません。");
    var coordinateReturnStart = shaderSource.IndexOf(
        "returnfloat4(bitValues,1.0f);",
        Math.Max(coordinateBranchStart, 0),
        StringComparison.Ordinal);
    var originalSampleStart = shaderSource.IndexOf(
        "OriginalTexture.Sample(",
        StringComparison.Ordinal);
    var blurredSampleStart = shaderSource.IndexOf(
        "BlurredTexture.Sample(",
        StringComparison.Ordinal);
    var maskSampleStart = shaderSource.IndexOf(
        "MaskTexture.SampleLevel(",
        StringComparison.Ordinal);
    True(
        coordinateReturnStart > coordinateBranchStart &&
        originalSampleStart > coordinateReturnStart &&
        blurredSampleStart > coordinateReturnStart &&
        maskSampleStart > coordinateReturnStart,
        "静止水滴セル座標ビット診断はOriginal、Blurred、Maskの全サンプリングより前に完結する必要があります。");
    var diagnosticBlock =
        shaderSource[diagnosticStart..mediumOnlyBranchStart] +
        shaderSource[cellSetupStart..hashInputBranchStart];
    foreach (var requiredExpression in new[]
             {
                 "booldiagnosticIsQuad=regionShape>=2.5f;",
                 "if(diagnosticIsQuad&&quadValid<0.5f)",
                 "returnfloat4(1.0f,0.0f,1.0f,1.0f);",
                 "float3diagnosticInputPoint=float3(inputUv,1.0f);",
                 "floatdiagnosticNumeratorU=dot(quadInverseRow0,diagnosticInputPoint);",
                 "floatdiagnosticNumeratorV=dot(quadInverseRow1,diagnosticInputPoint);",
                 "floatdiagnosticDenominator=dot(quadInverseRow2,diagnosticInputPoint);",
                 "if(abs(diagnosticDenominator)<0.000001f)",
                 "returnfloat4(1.0f,1.0f,0.0f,1.0f);",
                 "float2diagnosticSafeRegionSize=max(regionSize,float2(0.000001f,0.000001f));",
                 "float2diagnosticRegionSizePixels=diagnosticSafeRegionSize*safeInputSize;",
                 "float2diagnosticRegionCenterPixels=regionCenter*safeInputSize;",
                 "float2diagnosticLocalPixelPosition=RotateToRegionLocal(inputPixelPosition-diagnosticRegionCenterPixels);",
                 "diagnosticRegionUv=diagnosticLocalPixelPosition/diagnosticRegionSizePixels+0.5f;",
                 "float2diagnosticRegionPixelSize=max(regionSize*safeInputSize,float2(1.0f,1.0f));",
                 "float2diagnosticRegionPixelPosition=diagnosticRegionUv*diagnosticRegionPixelSize;",
                 "floatresolutionScale=max(safeInputSize.y/1080.0f,0.25f);",
                 "floatsizeScale=resolutionScale*max(outsideDropletSizeScale,0.25f);",
                 "float2staticPatternPosition=useScreenSpace>=0.5f?inputPixelPosition:diagnosticRegionPixelPosition;",
                 "float2smallCell=floor(staticPatternPosition/max(20.0f*sizeScale,1.0f));",
                 "float2mediumCell=floor(staticPatternPosition/max(44.0f*sizeScale,1.0f));",
                 "float2largeCell=floor(staticPatternPosition/max(92.0f*sizeScale,1.0f));",
                 "uintstripeIndex=(uint)floor(max(scenePosition.x,0.0f)/8.0f);",
                 "uintcomponentIndex=(stripeIndex>>5)&1u;",
                 "uintbitIndex=stripeIndex&31u;",
                 "uint3cellBits=componentIndex==0u?uint3(asuint(smallCell.x),asuint(mediumCell.x),asuint(largeCell.x)):uint3(asuint(smallCell.y),asuint(mediumCell.y),asuint(largeCell.y));",
                 "float3bitValues=float3((float)((cellBits.x>>bitIndex)&1u),(float)((cellBits.y>>bitIndex)&1u),(float)((cellBits.z>>bitIndex)&1u));",
                 "returnfloat4(bitValues,1.0f);",
             })
    {
        True(
            diagnosticBlock.Contains(requiredExpression, StringComparison.Ordinal),
            $"静止水滴セル座標ビット診断の可逆表示契約が不足しています: {requiredExpression}");
    }

    True(
        !diagnosticBlock.Contains("HashNoise2D", StringComparison.Ordinal) &&
        !diagnosticBlock.Contains("GetOutsideDropletCellStateFingerprint", StringComparison.Ordinal) &&
        !diagnosticBlock.Contains("outsideDropletSeed", StringComparison.Ordinal) &&
        !diagnosticBlock.Contains("sin(", StringComparison.Ordinal) &&
        !diagnosticBlock.Contains("fwidth", StringComparison.Ordinal) &&
        !diagnosticBlock.Contains("Texture.Sample", StringComparison.Ordinal) &&
        !diagnosticBlock.Contains("OriginalTexture.Sample", StringComparison.Ordinal) &&
        !diagnosticBlock.Contains("BlurredTexture.Sample", StringComparison.Ordinal) &&
        !diagnosticBlock.Contains("outsideDropletLocalTimeSeconds", StringComparison.Ordinal) &&
        !diagnosticBlock.Contains("GetOutsideDropletLighting", StringComparison.Ordinal),
        "静止水滴セル座標ビット診断へHash、Seed、導関数、画像サンプリング、落下時刻、照明を含めてはいけません。");

#if DEBUG
    Equal(
        12,
        (int)GlassWipeDebugView.OutsideDropletCellCoordinateBits,
        "Debug版の静止水滴セル座標ビット表示は値12である必要があります。");
    Equal(
        "静止セル座標ビット（診断）",
        GetDisplayName(GlassWipeDebugView.OutsideDropletCellCoordinateBits),
        "静止水滴セル座標ビット診断の表示名が不正です。");
    var description = GetDisplayDescription(
        GlassWipeDebugView.OutsideDropletCellCoordinateBits);
    True(
        description.Contains("マゼンタは無効な四角形", StringComparison.Ordinal) &&
        description.Contains("黄は座標計算不可", StringComparison.Ordinal) &&
        description.Contains("ビット判定を行わない", StringComparison.Ordinal),
        "静止水滴セル座標ビット診断は無効状態の色と判定保留を説明する必要があります。");
#else
    True(
        !Enum.IsDefined(typeof(GlassWipeDebugView), 12),
        "Release版へ静止水滴セル座標ビットの診断用Debug View値を公開してはいけません。");
    Equal(
        GlassWipeDebugView.Final,
        GlassWipeParameterSanitizer.SanitizeDebugView((GlassWipeDebugView)12),
        "Release版は静止水滴セル座標ビットの診断用値を最終結果へ補正する必要があります。");
#endif
}

static void VerifyOutsideDropletHashInputBitsDebugViewContract()
{
    var shaderSource = RemoveWhitespace(ShaderVerificationSource.ReadAllText(
        Path.Combine(
            FindRepositoryRoot(),
            "src",
            "YMM4GlassWipe",
            "Shaders",
            "GlassComposite.hlsl")));

    var diagnosticStart = shaderSource.IndexOf(
        "if(debugView>=10.5f&&debugView<23.5f)",
        StringComparison.Ordinal);
    var hashInputBranchStart = shaderSource.IndexOf(
        "if(debugView<13.5f)",
        Math.Max(diagnosticStart, 0),
        StringComparison.Ordinal);
    True(
        diagnosticStart >= 0 && hashInputBranchStart > diagnosticStart,
        "水滴Hash入力ビット診断の分岐が見つかりません。");
    var hashInputReturnStart = shaderSource.IndexOf(
        "returnfloat4(hashInputBitValues,1.0f);",
        Math.Max(hashInputBranchStart, 0),
        StringComparison.Ordinal);
    var finalHashBranchStart = shaderSource.IndexOf(
        "if(debugView<14.5f)",
        Math.Max(hashInputReturnStart, 0),
        StringComparison.Ordinal);
    var originalSampleStart = shaderSource.IndexOf(
        "OriginalTexture.Sample(",
        StringComparison.Ordinal);
    var blurredSampleStart = shaderSource.IndexOf(
        "BlurredTexture.Sample(",
        StringComparison.Ordinal);
    var maskSampleStart = shaderSource.IndexOf(
        "MaskTexture.SampleLevel(",
        StringComparison.Ordinal);
    True(
        hashInputReturnStart > hashInputBranchStart &&
        finalHashBranchStart > hashInputReturnStart &&
        originalSampleStart > hashInputReturnStart &&
        blurredSampleStart > hashInputReturnStart &&
        maskSampleStart > hashInputReturnStart,
        "水滴Hash入力ビット診断はOriginal、Blurred、Maskの全サンプリングより前に完結する必要があります。");
    var diagnosticBlock = shaderSource[hashInputBranchStart..finalHashBranchStart];

    foreach (var requiredExpression in new[]
             {
                 "uinthashOffsetIndex=(uint)floor(max(position.y,0.0f)/64.0f)%5u;",
                 "floathashOffset=hashOffsetIndex==0u?3.1f:hashOffsetIndex==1u?11.7f:hashOffsetIndex==2u?29.3f:hashOffsetIndex==3u?47.9f:61.1f;",
                 "floatsmallLayerSeed=outsideDropletSeed+101.0f;",
                 "floatmediumLayerSeed=outsideDropletSeed+307.0f;",
                 "floatlargeLayerSeed=outsideDropletSeed+701.0f;",
                 "GetOutsideDropletHashInputWithoutSine(smallCell,smallLayerSeed,hashOffset)",
                 "GetOutsideDropletHashInputWithoutSine(mediumCell,mediumLayerSeed,hashOffset)",
                 "GetOutsideDropletHashInputWithoutSine(largeCell,largeLayerSeed,hashOffset)",
                 "uint3hashInputBits=asuint(float3(",
                 "returnfloat4(hashInputBitValues,1.0f);",
             })
    {
        True(
            diagnosticBlock.Contains(requiredExpression, StringComparison.Ordinal),
            $"水滴Hash入力ビット診断の可逆表示契約が不足しています: {requiredExpression}");
    }

    var sharedBlock = shaderSource[diagnosticStart..hashInputBranchStart];
    True(
        sharedBlock.Contains("uintbitIndex=stripeIndex&31u;", StringComparison.Ordinal),
        "水滴Hash入力ビット診断は32ビットを8px縦縞で表示する必要があります。");
    True(
        !diagnosticBlock.Contains("MixOutsideDropletHashInputWithoutSine", StringComparison.Ordinal) &&
        !diagnosticBlock.Contains("HashNoise2D", StringComparison.Ordinal) &&
        !diagnosticBlock.Contains("sin(", StringComparison.Ordinal) &&
        !diagnosticBlock.Contains("fwidth", StringComparison.Ordinal) &&
        !diagnosticBlock.Contains("Texture.Sample", StringComparison.Ordinal) &&
        !diagnosticBlock.Contains("outsideDropletLocalTimeSeconds", StringComparison.Ordinal) &&
        !diagnosticBlock.Contains("GetOutsideDropletLighting", StringComparison.Ordinal),
        "水滴Hash入力ビット診断へ整数Mix、sin系Hash、導関数、画像サンプリング、落下時刻、照明を含めてはいけません。");

#if DEBUG
    Equal(
        13,
        (int)GlassWipeDebugView.OutsideDropletHashInputBits,
        "Debug版の水滴Hash入力ビット表示は値13である必要があります。");
    Equal(
        "Hash入力ビット（診断）",
        GetDisplayName(GlassWipeDebugView.OutsideDropletHashInputBits),
        "水滴Hash入力ビット診断の表示名が不正です。");
    var description = GetDisplayDescription(
        GlassWipeDebugView.OutsideDropletHashInputBits);
    True(
        description.Contains("64px高の横帯", StringComparison.Ordinal) &&
        description.Contains("320pxごとに繰り返し", StringComparison.Ordinal) &&
        description.Contains("横帯と縞の境界ではなく中央", StringComparison.Ordinal) &&
        description.Contains("マゼンタまたは黄の場合は判定せず", StringComparison.Ordinal),
        "水滴Hash入力ビット診断は固定5入力の配置と比較位置、無効状態を説明する必要があります。");
#else
    True(
        !Enum.IsDefined(typeof(GlassWipeDebugView), 13),
        "Release版へ水滴Hash入力ビットの診断用Debug View値を公開してはいけません。");
    Equal(
        GlassWipeDebugView.Final,
        GlassWipeParameterSanitizer.SanitizeDebugView((GlassWipeDebugView)13),
        "Release版は水滴Hash入力ビットの診断用値を最終結果へ補正する必要があります。");
#endif
}

static void VerifyOutsideDropletFinalHashBitsWithoutSineDebugViewContract()
{
    var shaderSource = RemoveWhitespace(ShaderVerificationSource.ReadAllText(
        Path.Combine(
            FindRepositoryRoot(),
            "src",
            "YMM4GlassWipe",
            "Shaders",
            "GlassComposite.hlsl")));

    var diagnosticStart = shaderSource.IndexOf(
        "if(debugView>=10.5f&&debugView<23.5f)",
        StringComparison.Ordinal);
    var hashInputReturnStart = shaderSource.IndexOf(
        "returnfloat4(hashInputBitValues,1.0f);",
        Math.Max(diagnosticStart, 0),
        StringComparison.Ordinal);
    var finalHashStart = shaderSource.IndexOf(
        "uint3finalFingerprintBits=uint3(",
        Math.Max(hashInputReturnStart, 0),
        StringComparison.Ordinal);
    var finalHashReturnStart = shaderSource.IndexOf(
        "returnfloat4(finalFingerprintBitValues,1.0f);",
        Math.Max(finalHashStart, 0),
        StringComparison.Ordinal);
    var mixedHashBranchStart = shaderSource.IndexOf(
        "if(debugView<16.5f)",
        Math.Max(finalHashReturnStart, 0),
        StringComparison.Ordinal);
    var originalSampleStart = shaderSource.IndexOf(
        "OriginalTexture.Sample(",
        StringComparison.Ordinal);
    var blurredSampleStart = shaderSource.IndexOf(
        "BlurredTexture.Sample(",
        StringComparison.Ordinal);
    var maskSampleStart = shaderSource.IndexOf(
        "MaskTexture.SampleLevel(",
        StringComparison.Ordinal);
    True(
        diagnosticStart >= 0 &&
        hashInputReturnStart > diagnosticStart &&
        finalHashStart > hashInputReturnStart &&
        finalHashReturnStart > finalHashStart &&
        mixedHashBranchStart > finalHashReturnStart,
        "sinなし水滴最終Hashビット診断の分岐が見つかりません。");
    True(
        shaderSource.Contains(
            "returnfloat4(hashInputBitValues,1.0f);}if(debugView<14.5f){uint3finalFingerprintBits=uint3(",
            StringComparison.Ordinal),
        "sinなし水滴最終Hashビット診断はDebug13分岐の外側に置く必要があります。");
    True(
        originalSampleStart > finalHashReturnStart &&
        blurredSampleStart > finalHashReturnStart &&
        maskSampleStart > finalHashReturnStart,
        "sinなし水滴最終Hashビット診断はOriginal、Blurred、Maskの全サンプリングより前に完結する必要があります。");

    var diagnosticBlock = shaderSource[finalHashStart..mixedHashBranchStart];
    foreach (var requiredExpression in new[]
             {
                 "GetOutsideDropletCellStateFingerprintBitsWithoutSine(staticPatternPosition,20.0f*sizeScale,outsideDropletSeed+101.0f)",
                 "GetOutsideDropletCellStateFingerprintBitsWithoutSine(staticPatternPosition,44.0f*sizeScale,outsideDropletSeed+307.0f)",
                 "GetOutsideDropletCellStateFingerprintBitsWithoutSine(staticPatternPosition,92.0f*sizeScale,outsideDropletSeed+701.0f)",
                 "constuintfinalFingerprintBitIndex=0u;",
                 "float3finalFingerprintBitValues=float3((float)((finalFingerprintBits.x>>finalFingerprintBitIndex)&1u),(float)((finalFingerprintBits.y>>finalFingerprintBitIndex)&1u),(float)((finalFingerprintBits.z>>finalFingerprintBitIndex)&1u));",
                 "returnfloat4(finalFingerprintBitValues,1.0f);",
             })
    {
        True(
            diagnosticBlock.Contains(requiredExpression, StringComparison.Ordinal),
            $"sinなし水滴最終Hashビット診断の可逆表示契約が不足しています: {requiredExpression}");
    }

    var sharedBlock = shaderSource[diagnosticStart..finalHashStart];
    True(
        sharedBlock.Contains(
            "float2staticPatternPosition=useScreenSpace>=0.5f?inputPixelPosition:diagnosticRegionPixelPosition;",
            StringComparison.Ordinal),
        "sinなし水滴最終Hash bit 0診断はDebug11と同じ座標を共有する必要があります。");
    True(
        diagnosticBlock.Contains(
            "constuintfinalFingerprintBitIndex=0u;",
            StringComparison.Ordinal) &&
        !diagnosticBlock.Contains(
            ">>bitIndex",
            StringComparison.Ordinal),
        "sinなし水滴最終Hash bit 0診断は画面X位置で表示ビットを切り替えてはいけません。");
    var fingerprintBitsFunction = ExtractHlslFunction(
        shaderSource,
        "uintGetOutsideDropletCellStateFingerprintBitsWithoutSine(");
    True(
        !diagnosticBlock.Contains("HashNoise2D", StringComparison.Ordinal) &&
        !diagnosticBlock.Contains("sin(", StringComparison.Ordinal) &&
        !diagnosticBlock.Contains("fwidth", StringComparison.Ordinal) &&
        !diagnosticBlock.Contains("Texture.Sample", StringComparison.Ordinal) &&
        !diagnosticBlock.Contains("outsideDropletLocalTimeSeconds", StringComparison.Ordinal) &&
        !diagnosticBlock.Contains("GetOutsideDropletLighting", StringComparison.Ordinal) &&
        !fingerprintBitsFunction.Contains("HashNoise2D", StringComparison.Ordinal) &&
        !fingerprintBitsFunction.Contains("sin(", StringComparison.Ordinal) &&
        !fingerprintBitsFunction.Contains("fwidth", StringComparison.Ordinal) &&
        !fingerprintBitsFunction.Contains("Texture.Sample", StringComparison.Ordinal) &&
        !fingerprintBitsFunction.Contains("outsideDropletLocalTimeSeconds", StringComparison.Ordinal) &&
        !fingerprintBitsFunction.Contains("GetOutsideDropletLighting", StringComparison.Ordinal),
        "sinなし水滴最終Hashビット診断へsin系Hash、導関数、画像サンプリング、落下時刻、照明を含めてはいけません。");

#if DEBUG
    Equal(
        14,
        (int)GlassWipeDebugView.OutsideDropletFinalHashBitsWithoutSine,
        "Debug版のsinなし水滴最終Hashビット表示は値14である必要があります。");
    Equal(
        "最終Hash bit 0（診断）",
        GetDisplayName(GlassWipeDebugView.OutsideDropletFinalHashBitsWithoutSine),
        "sinなし水滴最終Hashビット診断の表示名が不正です。");
    var description = GetDisplayDescription(
        GlassWipeDebugView.OutsideDropletFinalHashBitsWithoutSine);
    True(
        description.Contains("最下位ビット（bit 0）", StringComparison.Ordinal) &&
        description.Contains("画面X位置による表示ビットの切替は行いません", StringComparison.Ordinal) &&
        description.Contains("既存セルの中央色", StringComparison.Ordinal) &&
        description.Contains("マゼンタまたは黄の場合は判定せず", StringComparison.Ordinal),
        "sinなし水滴最終Hashビット診断は表示対象、配置、比較位置、無効状態を説明する必要があります。");
#else
    True(
        !Enum.IsDefined(typeof(GlassWipeDebugView), 14),
        "Release版へsinなし水滴最終Hashビットの診断用Debug View値を公開してはいけません。");
    Equal(
        GlassWipeDebugView.Final,
        GlassWipeParameterSanitizer.SanitizeDebugView((GlassWipeDebugView)14),
        "Release版はsinなし水滴最終Hashビットの診断用値を最終結果へ補正する必要があります。");
#endif
}

static void VerifyFixedReturnColorDebugViewContract()
{
    var shaderSource = RemoveWhitespace(ShaderVerificationSource.ReadAllText(
        Path.Combine(
            FindRepositoryRoot(),
            "src",
            "YMM4GlassWipe",
            "Shaders",
            "GlassComposite.hlsl")));

    var mainStart = shaderSource.IndexOf(
        "float4main(",
        StringComparison.Ordinal);
    var fixedBranchStart = shaderSource.IndexOf(
        "if(debugView>=14.5f&&debugView<15.5f)",
        Math.Max(mainStart, 0),
        StringComparison.Ordinal);
    var fixedReturnStart = shaderSource.IndexOf(
        "returnfloat4(0.25f,0.5f,0.75f,1.0f);",
        Math.Max(fixedBranchStart, 0),
        StringComparison.Ordinal);
    var coordinateCalculationStart = shaderSource.IndexOf(
        "float2safeInputSize=max(inputSize,float2(1.0f,1.0f));",
        Math.Max(mainStart, 0),
        StringComparison.Ordinal);
    var originalSampleStart = shaderSource.IndexOf(
        "OriginalTexture.Sample(",
        Math.Max(mainStart, 0),
        StringComparison.Ordinal);
    var blurredSampleStart = shaderSource.IndexOf(
        "BlurredTexture.Sample(",
        Math.Max(mainStart, 0),
        StringComparison.Ordinal);
    var maskSampleStart = shaderSource.IndexOf(
        "MaskTexture.SampleLevel(",
        Math.Max(mainStart, 0),
        StringComparison.Ordinal);

    True(
        mainStart >= 0 &&
        fixedBranchStart > mainStart &&
        fixedReturnStart > fixedBranchStart &&
        coordinateCalculationStart > fixedReturnStart,
        "固定色return診断は座標計算より前に完結する必要があります。");
    True(
        originalSampleStart > fixedReturnStart &&
        blurredSampleStart > fixedReturnStart &&
        maskSampleStart > fixedReturnStart,
        "固定色return診断はOriginal、Blurred、Maskの全サンプリングより前に完結する必要があります。");
    True(
        shaderSource.Contains(
            "if(debugView>=14.5f&&debugView<15.5f){returnfloat4(0.25f,0.5f,0.75f,1.0f);}",
            StringComparison.Ordinal),
        "固定色return診断は非二値の固定RGBAだけを返す必要があります。");

#if DEBUG
    Equal(
        15,
        (int)GlassWipeDebugView.FixedReturnColor,
        "Debug版の固定色return表示は値15である必要があります。");
    Equal(
        "固定色return（診断）",
        GetDisplayName(GlassWipeDebugView.FixedReturnColor),
        "固定色return診断の表示名が不正です。");
    var description = GetDisplayDescription(GlassWipeDebugView.FixedReturnColor);
    True(
        description.Contains("座標、Hash、画像を参照せず", StringComparison.Ordinal) &&
        description.Contains("全面が一様なまま", StringComparison.Ordinal) &&
        description.Contains("以前の模様が部分的に混ざる", StringComparison.Ordinal) &&
        description.Contains("確認後は最終結果へ戻してください", StringComparison.Ordinal),
        "固定色return診断は比較対象、異常状態、復帰手順を説明する必要があります。");
#else
    True(
        !Enum.IsDefined(typeof(GlassWipeDebugView), 15),
        "Release版へ固定色returnの診断用Debug View値を公開してはいけません。");
    Equal(
        GlassWipeDebugView.Final,
        GlassWipeParameterSanitizer.SanitizeDebugView((GlassWipeDebugView)15),
        "Release版は固定色returnの診断用値を最終結果へ補正する必要があります。");
#endif
}

static void VerifyOutsideDropletMixedHashBitsWithoutSineDebugViewContract()
{
    var shaderSource = RemoveWhitespace(ShaderVerificationSource.ReadAllText(
        Path.Combine(
            FindRepositoryRoot(),
            "src",
            "YMM4GlassWipe",
            "Shaders",
            "GlassComposite.hlsl")));

    var mixedBranchStart = shaderSource.IndexOf(
        "if(debugView<16.5f)",
        StringComparison.Ordinal);
    var mixedReturnStart = shaderSource.IndexOf(
        "returnfloat4(mixedHashBitValues,1.0f);",
        Math.Max(mixedBranchStart, 0),
        StringComparison.Ordinal);
    var preAvalancheStart = shaderSource.IndexOf(
        "uint3preAvalancheHashBits=uint3(",
        Math.Max(mixedReturnStart, 0),
        StringComparison.Ordinal);
    var originalSampleStart = shaderSource.IndexOf(
        "OriginalTexture.Sample(",
        StringComparison.Ordinal);
    True(
        mixedBranchStart >= 0 &&
        mixedReturnStart > mixedBranchStart &&
        preAvalancheStart > mixedReturnStart &&
        originalSampleStart > preAvalancheStart,
        "sinなし水滴Mix後ビット診断の分岐が見つかりません。");

    var diagnosticBlock = shaderSource[mixedBranchStart..preAvalancheStart];
    foreach (var requiredExpression in new[]
             {
                 "uintmixedHashOffsetIndex=(uint)floor(max(scenePosition.y,0.0f)/64.0f)%5u;",
                 "floatmixedHashOffset=mixedHashOffsetIndex==0u?3.1f:mixedHashOffsetIndex==1u?11.7f:mixedHashOffsetIndex==2u?29.3f:mixedHashOffsetIndex==3u?47.9f:61.1f;",
                 "MixOutsideDropletHashInputWithoutSine(smallCell,outsideDropletSeed+101.0f,mixedHashOffset)",
                 "MixOutsideDropletHashInputWithoutSine(mediumCell,outsideDropletSeed+307.0f,mixedHashOffset)",
                 "MixOutsideDropletHashInputWithoutSine(largeCell,outsideDropletSeed+701.0f,mixedHashOffset)",
                 "float3mixedHashBitValues=float3((float)((mixedHashBits.x>>bitIndex)&1u),(float)((mixedHashBits.y>>bitIndex)&1u),(float)((mixedHashBits.z>>bitIndex)&1u));",
                 "returnfloat4(mixedHashBitValues,1.0f);",
             })
    {
        True(
            diagnosticBlock.Contains(requiredExpression, StringComparison.Ordinal),
            "sinなし水滴Mix後ビット診断の契約が不足しています: " + requiredExpression);
    }

    True(
        !diagnosticBlock.Contains("HashNoise2D", StringComparison.Ordinal) &&
        !diagnosticBlock.Contains("sin(", StringComparison.Ordinal) &&
        !diagnosticBlock.Contains("fwidth", StringComparison.Ordinal) &&
        !diagnosticBlock.Contains("Texture.Sample", StringComparison.Ordinal) &&
        !diagnosticBlock.Contains("outsideDropletLocalTimeSeconds", StringComparison.Ordinal) &&
        !diagnosticBlock.Contains("GetOutsideDropletLighting", StringComparison.Ordinal),
        "sinなし水滴Mix後ビット診断へsin系Hash、導関数、画像サンプリング、落下時刻、照明を含めてはいけません。");

#if DEBUG
    Equal(
        16,
        (int)GlassWipeDebugView.OutsideDropletMixedHashBitsWithoutSine,
        "Debug版のsinなし水滴Mix後ビット表示は値16である必要があります。");
    Equal(
        "Mix後ビット（診断）",
        GetDisplayName(GlassWipeDebugView.OutsideDropletMixedHashBitsWithoutSine),
        "sinなし水滴Mix後ビット診断の表示名が不正です。");
    var description = GetDisplayDescription(
        GlassWipeDebugView.OutsideDropletMixedHashBitsWithoutSine);
    True(
        description.Contains("Mixした直後の32ビット", StringComparison.Ordinal) &&
        description.Contains("64px高の横帯ごとに5入力", StringComparison.Ordinal) &&
        description.Contains("横帯と縞の境界ではなく中央", StringComparison.Ordinal) &&
        description.Contains("確認後は最終結果へ戻してください", StringComparison.Ordinal),
        "sinなし水滴Mix後ビット診断は演算段階、配置、比較位置、復帰手順を説明する必要があります。");
#else
    True(
        !Enum.IsDefined(typeof(GlassWipeDebugView), 16),
        "Release版へsinなし水滴Mix後ビットの診断用Debug View値を公開してはいけません。");
    Equal(
        GlassWipeDebugView.Final,
        GlassWipeParameterSanitizer.SanitizeDebugView((GlassWipeDebugView)16),
        "Release版はsinなし水滴Mix後ビットの診断用値を最終結果へ補正する必要があります。");
#endif
}

static void VerifyOutsideDropletPreAvalancheHashBitsWithoutSineDebugViewContract()
{
    var shaderSource = RemoveWhitespace(ShaderVerificationSource.ReadAllText(
        Path.Combine(
            FindRepositoryRoot(),
            "src",
            "YMM4GlassWipe",
            "Shaders",
            "GlassComposite.hlsl")));
    var preAvalancheFunction = ExtractHlslFunction(
        shaderSource,
        "uintGetOutsideDropletCellStatePreAvalancheBitsWithoutSine(");
    foreach (var requiredExpression in new[]
             {
                 "uintfingerprint=MixOutsideDropletHashInputWithoutSine(cell,seed,3.1f);",
                 "fingerprint^=MixOutsideDropletHashInputWithoutSine(cell,seed,11.7f)*0x9e3779b9u;",
                 "fingerprint^=MixOutsideDropletHashInputWithoutSine(cell,seed,29.3f)*0x85ebca6bu;",
                 "fingerprint^=MixOutsideDropletHashInputWithoutSine(cell,seed,47.9f)*0xc2b2ae35u;",
                 "fingerprint^=MixOutsideDropletHashInputWithoutSine(cell,seed,61.1f)*0x27d4eb2fu;",
                 "returnfingerprint;",
             })
    {
        True(
            preAvalancheFunction.Contains(requiredExpression, StringComparison.Ordinal),
            "sinなし水滴合成後関数の契約が不足しています: " + requiredExpression);
    }
    True(
        !preAvalancheFunction.Contains("fingerprint^=fingerprint>>16;", StringComparison.Ordinal) &&
        !preAvalancheFunction.Contains("fingerprint*=0x7feb352du;", StringComparison.Ordinal) &&
        !preAvalancheFunction.Contains("uintfolded=", StringComparison.Ordinal),
        "sinなし水滴合成後関数へ最終avalancheまたはfoldを含めてはいけません。");

    var preAvalancheStart = shaderSource.IndexOf(
        "uint3preAvalancheHashBits=uint3(",
        StringComparison.Ordinal);
    var preAvalancheReturnStart = shaderSource.IndexOf(
        "returnfloat4(preAvalancheHashBitValues,1.0f);",
        Math.Max(preAvalancheStart, 0),
        StringComparison.Ordinal);
    var weightedBranchStart = shaderSource.IndexOf(
        "if(debugView<18.5f)",
        Math.Max(preAvalancheReturnStart, 0),
        StringComparison.Ordinal);
    var originalSampleStart = shaderSource.IndexOf(
        "OriginalTexture.Sample(",
        StringComparison.Ordinal);
    True(
        preAvalancheStart >= 0 &&
        preAvalancheReturnStart > preAvalancheStart &&
        weightedBranchStart > preAvalancheReturnStart &&
        originalSampleStart > weightedBranchStart,
        "sinなし水滴合成後ビット診断の分岐が見つかりません。");
    var diagnosticBlock = shaderSource[preAvalancheStart..weightedBranchStart];
    foreach (var requiredExpression in new[]
             {
                 "GetOutsideDropletCellStatePreAvalancheBitsWithoutSine(staticPatternPosition,20.0f*sizeScale,outsideDropletSeed+101.0f)",
                 "GetOutsideDropletCellStatePreAvalancheBitsWithoutSine(staticPatternPosition,44.0f*sizeScale,outsideDropletSeed+307.0f)",
                 "GetOutsideDropletCellStatePreAvalancheBitsWithoutSine(staticPatternPosition,92.0f*sizeScale,outsideDropletSeed+701.0f)",
                 "float3preAvalancheHashBitValues=float3((float)((preAvalancheHashBits.x>>bitIndex)&1u),(float)((preAvalancheHashBits.y>>bitIndex)&1u),(float)((preAvalancheHashBits.z>>bitIndex)&1u));",
                 "returnfloat4(preAvalancheHashBitValues,1.0f);",
             })
    {
        True(
            diagnosticBlock.Contains(requiredExpression, StringComparison.Ordinal),
            "sinなし水滴合成後ビット診断の契約が不足しています: " + requiredExpression);
    }
    True(
        !diagnosticBlock.Contains("HashNoise2D", StringComparison.Ordinal) &&
        !diagnosticBlock.Contains("sin(", StringComparison.Ordinal) &&
        !diagnosticBlock.Contains("fwidth", StringComparison.Ordinal) &&
        !diagnosticBlock.Contains("Texture.Sample", StringComparison.Ordinal) &&
        !diagnosticBlock.Contains("outsideDropletLocalTimeSeconds", StringComparison.Ordinal) &&
        !diagnosticBlock.Contains("GetOutsideDropletLighting", StringComparison.Ordinal),
        "sinなし水滴合成後ビット診断へsin系Hash、導関数、画像サンプリング、落下時刻、照明を含めてはいけません。");

#if DEBUG
    Equal(
        17,
        (int)GlassWipeDebugView.OutsideDropletPreAvalancheHashBitsWithoutSine,
        "Debug版のsinなし水滴合成後ビット表示は値17である必要があります。");
    Equal(
        "合成後ビット（診断）",
        GetDisplayName(GlassWipeDebugView.OutsideDropletPreAvalancheHashBitsWithoutSine),
        "sinなし水滴合成後ビット診断の表示名が不正です。");
    var description = GetDisplayDescription(
        GlassWipeDebugView.OutsideDropletPreAvalancheHashBitsWithoutSine);
    True(
        description.Contains("合成した直後かつ最終avalanche前の32ビット", StringComparison.Ordinal) &&
        description.Contains("8px幅の縦縞", StringComparison.Ordinal) &&
        description.Contains("縞の境界ではなく中央", StringComparison.Ordinal) &&
        description.Contains("確認後は最終結果へ戻してください", StringComparison.Ordinal),
        "sinなし水滴合成後ビット診断は演算段階、配置、比較位置、復帰手順を説明する必要があります。");
#else
    True(
        !Enum.IsDefined(typeof(GlassWipeDebugView), 17),
        "Release版へsinなし水滴合成後ビットの診断用Debug View値を公開してはいけません。");
    Equal(
        GlassWipeDebugView.Final,
        GlassWipeParameterSanitizer.SanitizeDebugView((GlassWipeDebugView)17),
        "Release版はsinなし水滴合成後ビットの診断用値を最終結果へ補正する必要があります。");
#endif
}

static void VerifyOutsideDropletWeightedHashBitsWithoutSineDebugViewContract()
{
    var shaderSource = RemoveWhitespace(ShaderVerificationSource.ReadAllText(
        Path.Combine(
            FindRepositoryRoot(),
            "src",
            "YMM4GlassWipe",
            "Shaders",
            "GlassComposite.hlsl")));

    var weightedBranchStart = shaderSource.IndexOf(
        "if(debugView<18.5f)",
        StringComparison.Ordinal);
    var weightedReturnStart = shaderSource.IndexOf(
        "returnfloat4(weightedHashBitValues,1.0f);",
        Math.Max(weightedBranchStart, 0),
        StringComparison.Ordinal);
    var originalSampleStart = shaderSource.IndexOf(
        "OriginalTexture.Sample(",
        StringComparison.Ordinal);
    True(
        weightedBranchStart >= 0 &&
        weightedReturnStart > weightedBranchStart &&
        originalSampleStart > weightedReturnStart,
        "sinなし水滴乗算後ビット診断の分岐が見つかりません。");

    var diagnosticBlock = shaderSource[weightedBranchStart..originalSampleStart];
    foreach (var requiredExpression in new[]
             {
                 "uintweightedHashTermIndex=(uint)floor(max(scenePosition.y,0.0f)/64.0f)%5u;",
                 "floatweightedHashOffset=weightedHashTermIndex==0u?3.1f:weightedHashTermIndex==1u?11.7f:weightedHashTermIndex==2u?29.3f:weightedHashTermIndex==3u?47.9f:61.1f;",
                 "uintweightedHashMultiplier=weightedHashTermIndex==0u?1u:weightedHashTermIndex==1u?0x9e3779b9u:weightedHashTermIndex==2u?0x85ebca6bu:weightedHashTermIndex==3u?0xc2b2ae35u:0x27d4eb2fu;",
                 "MixOutsideDropletHashInputWithoutSine(smallCell,outsideDropletSeed+101.0f,weightedHashOffset)",
                 "MixOutsideDropletHashInputWithoutSine(mediumCell,outsideDropletSeed+307.0f,weightedHashOffset)",
                 "MixOutsideDropletHashInputWithoutSine(largeCell,outsideDropletSeed+701.0f,weightedHashOffset)",
                 "))*weightedHashMultiplier;",
                 "float3weightedHashBitValues=float3((float)((weightedHashBits.x>>bitIndex)&1u),(float)((weightedHashBits.y>>bitIndex)&1u),(float)((weightedHashBits.z>>bitIndex)&1u));",
                 "returnfloat4(weightedHashBitValues,1.0f);",
             })
    {
        True(
            diagnosticBlock.Contains(requiredExpression, StringComparison.Ordinal),
            "sinなし水滴乗算後ビット診断の契約が不足しています: " + requiredExpression);
    }

    True(
        !diagnosticBlock.Contains("HashNoise2D", StringComparison.Ordinal) &&
        !diagnosticBlock.Contains("sin(", StringComparison.Ordinal) &&
        !diagnosticBlock.Contains("fwidth", StringComparison.Ordinal) &&
        !diagnosticBlock.Contains("Texture.Sample", StringComparison.Ordinal) &&
        !diagnosticBlock.Contains("outsideDropletLocalTimeSeconds", StringComparison.Ordinal) &&
        !diagnosticBlock.Contains("GetOutsideDropletLighting", StringComparison.Ordinal),
        "sinなし水滴乗算後ビット診断へsin系Hash、導関数、画像サンプリング、落下時刻、照明を含めてはいけません。");

#if DEBUG
    Equal(
        18,
        (int)GlassWipeDebugView.OutsideDropletWeightedHashBitsWithoutSine,
        "Debug版のsinなし水滴乗算後ビット表示は値18である必要があります。");
    Equal(
        "乗算後ビット（診断）",
        GetDisplayName(GlassWipeDebugView.OutsideDropletWeightedHashBitsWithoutSine),
        "sinなし水滴乗算後ビット診断の表示名が不正です。");
    var description = GetDisplayDescription(
        GlassWipeDebugView.OutsideDropletWeightedHashBitsWithoutSine);
    True(
        description.Contains("合成係数を個別に乗算した直後の32ビット", StringComparison.Ordinal) &&
        description.Contains("第1項の係数は1", StringComparison.Ordinal) &&
        description.Contains("赤は小水滴、緑は中水滴、青は大水滴", StringComparison.Ordinal) &&
        description.Contains("8px幅の縦縞", StringComparison.Ordinal) &&
        description.Contains("64px高の横帯ごとに第1項から第5項", StringComparison.Ordinal) &&
        description.Contains("横帯と縞の境界ではなく中央", StringComparison.Ordinal) &&
        description.Contains("確認後は最終結果へ戻してください", StringComparison.Ordinal),
        "sinなし水滴乗算後ビット診断は演算段階、RGB、配置、比較位置、復帰手順を説明する必要があります。");
#else
    True(
        !Enum.IsDefined(typeof(GlassWipeDebugView), 18),
        "Release版へsinなし水滴乗算後ビットの診断用Debug View値を公開してはいけません。");
    Equal(
        GlassWipeDebugView.Final,
        GlassWipeParameterSanitizer.SanitizeDebugView((GlassWipeDebugView)18),
        "Release版はsinなし水滴乗算後ビットの診断用値を最終結果へ補正する必要があります。");
#endif
}

static void VerifyOutsideDropletMediumOnlyHashInputStagesDebugViewContract()
{
    var shaderSource = RemoveWhitespace(ShaderVerificationSource.ReadAllText(
        Path.Combine(
            FindRepositoryRoot(),
            "src",
            "YMM4GlassWipe",
            "Shaders",
            "GlassComposite.hlsl")));

    var diagnosticBranchStart = shaderSource.IndexOf(
        "if(debugView>=18.5f&&debugView<19.5f)",
        StringComparison.Ordinal);
    var mediumCellStart = shaderSource.IndexOf(
        "float2mediumOnlyCell=",
        Math.Max(diagnosticBranchStart, 0),
        StringComparison.Ordinal);
    var stripeIndexStart = shaderSource.IndexOf(
        "uintmediumOnlyStripeIndex=",
        Math.Max(mediumCellStart, 0),
        StringComparison.Ordinal);
    var bitIndexStart = shaderSource.IndexOf(
        "uintmediumOnlyBitIndex=",
        Math.Max(stripeIndexStart, 0),
        StringComparison.Ordinal);
    var stageIndexStart = shaderSource.IndexOf(
        "uintmediumOnlyHashInputStage=",
        Math.Max(bitIndexStart, 0),
        StringComparison.Ordinal);
    var latticeDotStart = shaderSource.IndexOf(
        "floatmediumOnlyLatticeDot=",
        Math.Max(stageIndexStart, 0),
        StringComparison.Ordinal);
    var stageValueStart = shaderSource.IndexOf(
        "floatmediumOnlyHashInputStageValue=",
        Math.Max(latticeDotStart, 0),
        StringComparison.Ordinal);
    var stageBitsStart = shaderSource.IndexOf(
        "uintmediumOnlyHashInputStageBits=asuint(mediumOnlyHashInputStageValue);",
        Math.Max(stageValueStart, 0),
        StringComparison.Ordinal);
    var stageBitValueStart = shaderSource.IndexOf(
        "floatmediumOnlyHashInputStageBitValue=",
        Math.Max(stageBitsStart, 0),
        StringComparison.Ordinal);
    var stageBitReturnStart = shaderSource.IndexOf(
        "returnfloat4(mediumOnlyHashInputStageBitValue,mediumOnlyHashInputStageBitValue,mediumOnlyHashInputStageBitValue,1.0f);",
        Math.Max(stageBitValueStart, 0),
        StringComparison.Ordinal);
    var nextDiagnosticBranchStart = shaderSource.IndexOf(
        "if(debugView>=19.5f&&debugView<20.5f)",
        Math.Max(stageBitReturnStart, 0),
        StringComparison.Ordinal);
    var sharedSmallCellStart = shaderSource.IndexOf(
        "float2smallCell=",
        Math.Max(nextDiagnosticBranchStart, 0),
        StringComparison.Ordinal);
    True(
        diagnosticBranchStart >= 0 &&
        mediumCellStart > diagnosticBranchStart &&
        stripeIndexStart > mediumCellStart &&
        bitIndexStart > stripeIndexStart &&
        stageIndexStart > bitIndexStart &&
        latticeDotStart > stageIndexStart &&
        stageValueStart > latticeDotStart &&
        stageBitsStart > stageValueStart &&
        stageBitValueStart > stageBitsStart &&
        stageBitReturnStart > stageBitValueStart &&
        nextDiagnosticBranchStart > stageBitReturnStart &&
        sharedSmallCellStart > nextDiagnosticBranchStart,
        "水滴Hash入力加算段階bitの中セル単独診断が、値20と共通の小・中・大セル計算より前に正しい順序で見つかりません。");

    var diagnosticBlock = shaderSource[diagnosticBranchStart..nextDiagnosticBranchStart];
    foreach (var requiredExpression in new[]
             {
                 "float2mediumOnlyCell=floor(staticPatternPosition/max(44.0f*sizeScale,1.0f));",
                 "uintmediumOnlyStripeIndex=(uint)floor(max(scenePosition.x,0.0f)/8.0f);",
                 "uintmediumOnlyBitIndex=mediumOnlyStripeIndex&31u;",
                 "uintmediumOnlyHashInputStage=(uint)floor(max(scenePosition.y,0.0f)/64.0f)%3u;",
                 "floatmediumOnlyLatticeDot=dot(mediumOnlyCell,float2(127.1f,311.7f));",
                 "floatmediumOnlyHashInputStageValue=mediumOnlyHashInputStage==0u?mediumOnlyLatticeDot:mediumOnlyHashInputStage==1u?mediumOnlyLatticeDot+(outsideDropletSeed+307.0f)*74.7f:mediumOnlyLatticeDot+((outsideDropletSeed+307.0f)+3.1f)*74.7f;",
                 "uintmediumOnlyHashInputStageBits=asuint(mediumOnlyHashInputStageValue);",
                 "floatmediumOnlyHashInputStageBitValue=(float)((mediumOnlyHashInputStageBits>>mediumOnlyBitIndex)&1u);",
                 "returnfloat4(mediumOnlyHashInputStageBitValue,mediumOnlyHashInputStageBitValue,mediumOnlyHashInputStageBitValue,1.0f);",
             })
    {
        True(
            diagnosticBlock.Contains(requiredExpression, StringComparison.Ordinal),
            "水滴Hash入力加算段階bitの中セル単独診断契約が不足しています: " + requiredExpression);
    }

    Equal(
        1,
        diagnosticBlock.Split("asuint(", StringSplitOptions.None).Length - 1,
        "水滴Hash入力加算段階bitの中セル単独診断では、選択済みの段階値だけをasuintする必要があります。");
    Equal(
        1,
        diagnosticBlock.Split("returnfloat4(", StringSplitOptions.None).Length - 1,
        "水滴Hash入力加算段階bitの中セル単独診断では、選択済みの段階bitだけを返す必要があります。");

    True(
        !diagnosticBlock.Contains("GetOutsideDropletHashInputWithoutSine(", StringComparison.Ordinal) &&
        !diagnosticBlock.Contains("MixOutsideDropletHashInputWithoutSine(", StringComparison.Ordinal) &&
        !diagnosticBlock.Contains("smallCell", StringComparison.Ordinal) &&
        !diagnosticBlock.Contains("largeCell", StringComparison.Ordinal) &&
        !diagnosticBlock.Contains("20.0f*sizeScale", StringComparison.Ordinal) &&
        !diagnosticBlock.Contains("92.0f*sizeScale", StringComparison.Ordinal) &&
        !diagnosticBlock.Contains("+101.0f", StringComparison.Ordinal) &&
        !diagnosticBlock.Contains("+701.0f", StringComparison.Ordinal) &&
        !diagnosticBlock.Contains("uint3", StringComparison.Ordinal) &&
        !diagnosticBlock.Contains("float3", StringComparison.Ordinal) &&
        !diagnosticBlock.Contains("mediumOnlyComponentIndex", StringComparison.Ordinal) &&
        !diagnosticBlock.Contains("mediumOnlyCellBits", StringComparison.Ordinal) &&
        !diagnosticBlock.Contains("accumulatedHashStageIndex", StringComparison.Ordinal) &&
        !diagnosticBlock.Contains("^=", StringComparison.Ordinal) &&
        !diagnosticBlock.Contains("*11.7f", StringComparison.Ordinal) &&
        !diagnosticBlock.Contains("*29.3f", StringComparison.Ordinal) &&
        !diagnosticBlock.Contains("*47.9f", StringComparison.Ordinal) &&
        !diagnosticBlock.Contains("*61.1f", StringComparison.Ordinal) &&
        !diagnosticBlock.Contains("0x9e3779b9u", StringComparison.Ordinal) &&
        !diagnosticBlock.Contains("0x85ebca6bu", StringComparison.Ordinal) &&
        !diagnosticBlock.Contains("0xc2b2ae35u", StringComparison.Ordinal) &&
        !diagnosticBlock.Contains("0x27d4eb2fu", StringComparison.Ordinal),
        "水滴Hash入力加算段階bitの中セル単独診断へhelper、Mix、小・大セル、packedな一時値または第2～5項を含めてはいけません。");

    True(
        !diagnosticBlock.Contains("HashNoise2D", StringComparison.Ordinal) &&
        !diagnosticBlock.Contains("sin(", StringComparison.Ordinal) &&
        !diagnosticBlock.Contains("fwidth", StringComparison.Ordinal) &&
        !diagnosticBlock.Contains("Texture.Sample", StringComparison.Ordinal) &&
        !diagnosticBlock.Contains("OriginalTexture", StringComparison.Ordinal) &&
        !diagnosticBlock.Contains("BlurredTexture", StringComparison.Ordinal) &&
        !diagnosticBlock.Contains("MaskTexture", StringComparison.Ordinal) &&
        !diagnosticBlock.Contains("outsideDropletLocalTimeSeconds", StringComparison.Ordinal) &&
        !diagnosticBlock.Contains("GetOutsideDropletLighting", StringComparison.Ordinal),
        "水滴Hash入力加算段階bitの中セル単独診断へsin系Hash、導関数、画像サンプリング、落下時刻、照明を含めてはいけません。");

    var referenceCell = new Vector2(12.0f, -7.0f);
    const float referenceSeed = 20.0f;
    var referenceStage0 = Vector2.Dot(referenceCell, new Vector2(127.1f, 311.7f));
    var referenceStage1 = referenceStage0 + (referenceSeed + 307.0f) * 74.7f;
    var referenceStage2 = referenceStage0 + ((referenceSeed + 307.0f) + 3.1f) * 74.7f;
    True(
        new[]
        {
            BitConverter.SingleToUInt32Bits(referenceStage0),
            BitConverter.SingleToUInt32Bits(referenceStage1),
            BitConverter.SingleToUInt32Bits(referenceStage2),
        }.Distinct().Count() == 3,
        "水滴Hash入力加算段階bitの数値参照では、dot、Seed項加算後、offset込み完全式の3段階が異なるbit列になる必要があります。");

    var stageBandIndices = new[] { 0.0f, 63.0f, 64.0f, 127.0f, 128.0f, 191.0f, 192.0f }
        .Select(y => (uint)MathF.Floor(MathF.Max(y, 0.0f) / 64.0f) % 3u)
        .ToArray();
    True(
        stageBandIndices.SequenceEqual(new uint[] { 0u, 0u, 1u, 1u, 2u, 2u, 0u }),
        "水滴Hash入力加算段階bitは64px高の3帯を192pxごとに繰り返す必要があります。");

#if DEBUG
    Equal(
        19,
        (int)GlassWipeDebugView.OutsideDropletAccumulatedHashBitsWithoutSine,
        "Debug版の水滴Hash入力加算段階bitの中セル単独表示は値19である必要があります。");
    Equal(
        "中セル単独・Hash入力加算段階bit（診断）",
        GetDisplayName(GlassWipeDebugView.OutsideDropletAccumulatedHashBitsWithoutSine),
        "水滴Hash入力加算段階bitの中セル単独診断の表示名が不正です。");
    var description = GetDisplayDescription(
        GlassWipeDebugView.OutsideDropletAccumulatedHashBitsWithoutSine);
    True(
        description.Contains("中水滴セルだけを44px基準で計算", StringComparison.Ordinal) &&
        description.Contains("64px高×3段階", StringComparison.Ordinal) &&
        description.Contains("上からdotだけ", StringComparison.Ordinal) &&
        description.Contains("(Seed+307)×74.7を加算", StringComparison.Ordinal) &&
        description.Contains("offset 3.1込みの完全式", StringComparison.Ordinal) &&
        description.Contains("192pxごとに繰り返します", StringComparison.Ordinal) &&
        description.Contains("asuintで32bit", StringComparison.Ordinal) &&
        description.Contains("R=G=Bのグレースケール", StringComparison.Ordinal) &&
        description.Contains("8px幅の縦縞", StringComparison.Ordinal) &&
        description.Contains("画面全体", StringComparison.Ordinal) &&
        description.Contains("Hash入力helper、Mix、小水滴・大水滴セルを使いません", StringComparison.Ordinal) &&
        description.Contains("4枚のスクリーンショット", StringComparison.Ordinal) &&
        description.Contains("判定は画像差分", StringComparison.Ordinal) &&
        description.Contains("確認後は最終結果へ戻してください", StringComparison.Ordinal),
        "水滴Hash入力加算段階bitの中セル単独診断は対象成分、3段階、単独性、グレースケール、配置、記録方法、判定方法、復帰手順を説明する必要があります。");
#else
    True(
        !Enum.IsDefined(typeof(GlassWipeDebugView), 19),
        "Release版へ水滴Hash入力加算段階bitの中セル単独診断用Debug View値を公開してはいけません。");
    Equal(
        GlassWipeDebugView.Final,
        GlassWipeParameterSanitizer.SanitizeDebugView((GlassWipeDebugView)19),
        "Release版は水滴Hash入力加算段階bitの中セル単独診断用値を最終結果へ補正する必要があります。");
#endif
}

static void VerifyOutsideDropletMediumOnlyHashInputGreenBitDebugViewContract()
{
    var shaderSource = RemoveWhitespace(ShaderVerificationSource.ReadAllText(Path.Combine(
        FindRepositoryRoot(), "src", "YMM4GlassWipe", "Shaders", "GlassComposite.hlsl")));

    var diagnosticRangeStart = shaderSource.IndexOf(
        "if(debugView>=10.5f&&debugView<23.5f)",
        StringComparison.Ordinal);
    var diagnosticBranchStart = shaderSource.IndexOf(
        "if(debugView>=19.5f&&debugView<20.5f)",
        Math.Max(diagnosticRangeStart, 0),
        StringComparison.Ordinal);
    var mediumCellStart = shaderSource.IndexOf(
        "float2mediumOnlyCell=",
        Math.Max(diagnosticBranchStart, 0),
        StringComparison.Ordinal);
    var stripeIndexStart = shaderSource.IndexOf(
        "uintmediumOnlyStripeIndex=",
        Math.Max(mediumCellStart, 0),
        StringComparison.Ordinal);
    var bitIndexStart = shaderSource.IndexOf(
        "uintmediumOnlyBitIndex=",
        Math.Max(stripeIndexStart, 0),
        StringComparison.Ordinal);
    var hashInputStart = shaderSource.IndexOf(
        "floatmediumOnlyHashInput=GetOutsideDropletHashInputWithoutSine(",
        Math.Max(bitIndexStart, 0),
        StringComparison.Ordinal);
    var hashInputBitsStart = shaderSource.IndexOf(
        "uintmediumOnlyHashInputBits=asuint(mediumOnlyHashInput);",
        Math.Max(hashInputStart, 0),
        StringComparison.Ordinal);
    var hashInputBitValueStart = shaderSource.IndexOf(
        "floatmediumOnlyHashInputBitValue=",
        Math.Max(hashInputBitsStart, 0),
        StringComparison.Ordinal);
    var hashInputBitReturnStart = shaderSource.IndexOf(
        "returnfloat4(0.0f,mediumOnlyHashInputBitValue,0.0f,1.0f);",
        Math.Max(hashInputBitValueStart, 0),
        StringComparison.Ordinal);
    var nextDiagnosticBranchStart = shaderSource.IndexOf(
        "if(debugView>=20.5f&&debugView<21.5f)",
        Math.Max(hashInputBitReturnStart, 0),
        StringComparison.Ordinal);
    var sharedSmallCellStart = shaderSource.IndexOf(
        "float2smallCell=",
        Math.Max(nextDiagnosticBranchStart, 0),
        StringComparison.Ordinal);
    True(
        diagnosticRangeStart >= 0 &&
        diagnosticBranchStart > diagnosticRangeStart &&
        mediumCellStart > diagnosticBranchStart &&
        stripeIndexStart > mediumCellStart &&
        bitIndexStart > stripeIndexStart &&
        hashInputStart > bitIndexStart &&
        hashInputBitsStart > hashInputStart &&
        hashInputBitValueStart > hashInputBitsStart &&
        hashInputBitReturnStart > hashInputBitValueStart &&
        nextDiagnosticBranchStart > hashInputBitReturnStart &&
        sharedSmallCellStart > nextDiagnosticBranchStart,
        "水滴Hash入力bit G単独診断が、共通の小・中・大セル計算より前に正しい順序で見つかりません。");

    var diagnosticBlock = shaderSource[diagnosticBranchStart..nextDiagnosticBranchStart];
    foreach (var requiredExpression in new[]
             {
                 "float2mediumOnlyCell=floor(staticPatternPosition/max(44.0f*sizeScale,1.0f));",
                 "uintmediumOnlyStripeIndex=(uint)floor(max(scenePosition.x,0.0f)/8.0f);",
                 "uintmediumOnlyBitIndex=mediumOnlyStripeIndex&31u;",
                 "floatmediumOnlyHashInput=GetOutsideDropletHashInputWithoutSine(mediumOnlyCell,outsideDropletSeed+307.0f,3.1f);",
                 "uintmediumOnlyHashInputBits=asuint(mediumOnlyHashInput);",
                 "floatmediumOnlyHashInputBitValue=(float)((mediumOnlyHashInputBits>>mediumOnlyBitIndex)&1u);",
                 "returnfloat4(0.0f,mediumOnlyHashInputBitValue,0.0f,1.0f);",
             })
    {
        True(
            diagnosticBlock.Contains(requiredExpression, StringComparison.Ordinal),
            "水滴Hash入力bit G単独診断契約が不足しています: " + requiredExpression);
    }

    Equal(
        1,
        diagnosticBlock.Split("GetOutsideDropletHashInputWithoutSine(", StringSplitOptions.None).Length - 1,
        "水滴Hash入力bit G単独診断ではHash入力helperを1回だけ呼び出す必要があります。");
    Equal(
        1,
        diagnosticBlock.Split("asuint(", StringSplitOptions.None).Length - 1,
        "水滴Hash入力bit G単独診断ではhelperの戻り値だけをasuintする必要があります。");
    Equal(
        1,
        diagnosticBlock.Split("returnfloat4(", StringSplitOptions.None).Length - 1,
        "水滴Hash入力bit G単独診断ではG単独の値だけを返す必要があります。");
    True(
        !diagnosticBlock.Contains("MixOutsideDropletHashInputWithoutSine(", StringComparison.Ordinal) &&
        !diagnosticBlock.Contains("smallCell", StringComparison.Ordinal) &&
        !diagnosticBlock.Contains("largeCell", StringComparison.Ordinal) &&
        !diagnosticBlock.Contains("20.0f*sizeScale", StringComparison.Ordinal) &&
        !diagnosticBlock.Contains("92.0f*sizeScale", StringComparison.Ordinal) &&
        !diagnosticBlock.Contains("+101.0f", StringComparison.Ordinal) &&
        !diagnosticBlock.Contains("+701.0f", StringComparison.Ordinal) &&
        !diagnosticBlock.Contains("uint3", StringComparison.Ordinal) &&
        !diagnosticBlock.Contains("float3", StringComparison.Ordinal) &&
        !diagnosticBlock.Contains("scenePosition.y", StringComparison.Ordinal) &&
        !diagnosticBlock.Contains("/64.0f", StringComparison.Ordinal) &&
        !diagnosticBlock.Contains("%3u", StringComparison.Ordinal),
        "水滴Hash入力bit G単独診断へMix、小・大セル、別Seed、uint3、64px段階またはRGB同値出力を含めてはいけません。");
    True(
        !diagnosticBlock.Contains("HashNoise2D", StringComparison.Ordinal) &&
        !diagnosticBlock.Contains("sin(", StringComparison.Ordinal) &&
        !diagnosticBlock.Contains("fwidth", StringComparison.Ordinal) &&
        !diagnosticBlock.Contains("Texture.Sample", StringComparison.Ordinal) &&
        !diagnosticBlock.Contains("OriginalTexture", StringComparison.Ordinal) &&
        !diagnosticBlock.Contains("BlurredTexture", StringComparison.Ordinal) &&
        !diagnosticBlock.Contains("MaskTexture", StringComparison.Ordinal) &&
        !diagnosticBlock.Contains("outsideDropletLocalTimeSeconds", StringComparison.Ordinal) &&
        !diagnosticBlock.Contains("GetOutsideDropletLighting", StringComparison.Ordinal),
        "水滴Hash入力bit G単独診断へsin系Hash、導関数、画像サンプリング、落下時刻または照明を含めてはいけません。");

    var referenceHashInputBits = GetOutsideDropletHashInputBitsReference(
        new Vector2(12.0f, -7.0f),
        20.0f + 307.0f,
        3.1f);
    var referenceBitValues = Enumerable.Range(0, 32)
        .Select(bitIndex => (referenceHashInputBits >> bitIndex) & 1u)
        .ToArray();
    True(
        referenceBitValues.Contains(0u) && referenceBitValues.Contains(1u),
        "水滴Hash入力bit G単独診断のCPU参照は、helper入力の32ビットから0と1を抽出できる必要があります。");

#if DEBUG
    Equal(
        20,
        (int)GlassWipeDebugView.OutsideDropletMediumOnlyHashInputGreenBitWithoutSine,
        "Debug版の水滴Hash入力bit G単独表示は値20である必要があります。");
    Equal(
        "中セル単独・Hash入力bit G単独（診断）",
        GetDisplayName(GlassWipeDebugView.OutsideDropletMediumOnlyHashInputGreenBitWithoutSine),
        "水滴Hash入力bit G単独診断の表示名が不正です。");
    var description = GetDisplayDescription(
        GlassWipeDebugView.OutsideDropletMediumOnlyHashInputGreenBitWithoutSine);
    True(
        description.Contains("中水滴セルだけを44px基準で計算", StringComparison.Ordinal) &&
        description.Contains("GetOutsideDropletHashInputWithoutSine(cell, Seed+307, 3.1)", StringComparison.Ordinal) &&
        description.Contains("1回だけ", StringComparison.Ordinal) &&
        description.Contains("asuintで32bit", StringComparison.Ordinal) &&
        description.Contains("scenePosition.x由来", StringComparison.Ordinal) &&
        description.Contains("8px幅の縦縞", StringComparison.Ordinal) &&
        description.Contains("緑だけ", StringComparison.Ordinal) &&
        description.Contains("赤と青は0、alphaは1", StringComparison.Ordinal) &&
        description.Contains("64px高の段階、RGBグレースケール、Mix、小水滴・大水滴セル、uint3を使いません", StringComparison.Ordinal) &&
        description.Contains("4枚のスクリーンショット", StringComparison.Ordinal) &&
        description.Contains("判定は画像差分", StringComparison.Ordinal) &&
        description.Contains("確認後は最終結果へ戻してください", StringComparison.Ordinal),
        "水滴Hash入力bit G単独診断は対象、helper呼出し、bit選択、G出力、除外要素、記録方法、判定方法、復帰手順を説明する必要があります。");
#else
    True(
        !Enum.IsDefined(typeof(GlassWipeDebugView), 20),
        "Release版へ水滴Hash入力bit G単独診断用Debug View値を公開してはいけません。");
    Equal(
        GlassWipeDebugView.Final,
        GlassWipeParameterSanitizer.SanitizeDebugView((GlassWipeDebugView)20),
        "Release版は水滴Hash入力bit G単独診断用値を最終結果へ補正する必要があります。");
#endif
}

static void VerifyOutsideDropletMediumOnlyCellXGreenBitDebugViewContract()
{
    var shaderSource = RemoveWhitespace(ShaderVerificationSource.ReadAllText(Path.Combine(
        FindRepositoryRoot(), "src", "YMM4GlassWipe", "Shaders", "GlassComposite.hlsl")));

    var diagnosticRangeStart = shaderSource.IndexOf(
        "if(debugView>=10.5f&&debugView<23.5f)",
        StringComparison.Ordinal);
    var previousDiagnosticBranchStart = shaderSource.IndexOf(
        "if(debugView>=19.5f&&debugView<20.5f)",
        Math.Max(diagnosticRangeStart, 0),
        StringComparison.Ordinal);
    var diagnosticBranchStart = shaderSource.IndexOf(
        "if(debugView>=20.5f&&debugView<21.5f)",
        Math.Max(previousDiagnosticBranchStart, 0),
        StringComparison.Ordinal);
    var mediumCellStart = shaderSource.IndexOf(
        "float2mediumOnlyCell=",
        Math.Max(diagnosticBranchStart, 0),
        StringComparison.Ordinal);
    var stripeIndexStart = shaderSource.IndexOf(
        "uintmediumOnlyCellXStripeIndex=",
        Math.Max(mediumCellStart, 0),
        StringComparison.Ordinal);
    var bitIndexStart = shaderSource.IndexOf(
        "uintmediumOnlyCellXBitIndex=",
        Math.Max(stripeIndexStart, 0),
        StringComparison.Ordinal);
    var cellBitsStart = shaderSource.IndexOf(
        "uintmediumOnlyCellXBits=asuint(mediumOnlyCell.x);",
        Math.Max(bitIndexStart, 0),
        StringComparison.Ordinal);
    var cellBitValueStart = shaderSource.IndexOf(
        "floatmediumOnlyCellXBitValue=",
        Math.Max(cellBitsStart, 0),
        StringComparison.Ordinal);
    var cellBitReturnStart = shaderSource.IndexOf(
        "returnfloat4(0.0f,mediumOnlyCellXBitValue,0.0f,1.0f);",
        Math.Max(cellBitValueStart, 0),
        StringComparison.Ordinal);
    var nextDiagnosticBranchStart = shaderSource.IndexOf(
        "if(debugView>=21.5f&&debugView<22.5f)",
        Math.Max(cellBitReturnStart, 0),
        StringComparison.Ordinal);
    var sharedSmallCellStart = shaderSource.IndexOf(
        "float2smallCell=",
        Math.Max(nextDiagnosticBranchStart, 0),
        StringComparison.Ordinal);
    True(
        diagnosticRangeStart >= 0 &&
        previousDiagnosticBranchStart > diagnosticRangeStart &&
        diagnosticBranchStart > previousDiagnosticBranchStart &&
        mediumCellStart > diagnosticBranchStart &&
        stripeIndexStart > mediumCellStart &&
        bitIndexStart > stripeIndexStart &&
        cellBitsStart > bitIndexStart &&
        cellBitValueStart > cellBitsStart &&
        cellBitReturnStart > cellBitValueStart &&
        nextDiagnosticBranchStart > cellBitReturnStart &&
        sharedSmallCellStart > nextDiagnosticBranchStart,
        "水滴cell X bit G単独診断が、値20と値22の間、かつ共通の小・中・大セル計算より前に正しい順序で見つかりません。");

    var diagnosticBlock = shaderSource[diagnosticBranchStart..nextDiagnosticBranchStart];
    foreach (var requiredExpression in new[]
             {
                 "float2mediumOnlyCell=floor(staticPatternPosition/max(44.0f*sizeScale,1.0f));",
                 "uintmediumOnlyCellXStripeIndex=(uint)floor(max(scenePosition.x,0.0f)/8.0f);",
                 "uintmediumOnlyCellXBitIndex=mediumOnlyCellXStripeIndex&31u;",
                 "uintmediumOnlyCellXBits=asuint(mediumOnlyCell.x);",
                 "floatmediumOnlyCellXBitValue=(float)((mediumOnlyCellXBits>>mediumOnlyCellXBitIndex)&1u);",
                 "returnfloat4(0.0f,mediumOnlyCellXBitValue,0.0f,1.0f);",
             })
    {
        True(
            diagnosticBlock.Contains(requiredExpression, StringComparison.Ordinal),
            "水滴cell X bit G単独診断契約が不足しています: " + requiredExpression);
    }

    Equal(
        1,
        diagnosticBlock.Split("asuint(", StringSplitOptions.None).Length - 1,
        "水滴cell X bit G単独診断では中セルX座標だけをasuintする必要があります。");
    Equal(
        1,
        diagnosticBlock.Split("returnfloat4(", StringSplitOptions.None).Length - 1,
        "水滴cell X bit G単独診断ではG単独の値だけを返す必要があります。");
    True(
        !diagnosticBlock.Contains("GetOutsideDropletHashInputWithoutSine(", StringComparison.Ordinal) &&
        !diagnosticBlock.Contains("MixOutsideDropletHashInputWithoutSine(", StringComparison.Ordinal) &&
        !diagnosticBlock.Contains("dot(", StringComparison.Ordinal) &&
        !diagnosticBlock.Contains("outsideDropletSeed", StringComparison.Ordinal) &&
        !diagnosticBlock.Contains("mediumOnlyHashInput", StringComparison.Ordinal) &&
        !diagnosticBlock.Contains("smallCell", StringComparison.Ordinal) &&
        !diagnosticBlock.Contains("largeCell", StringComparison.Ordinal) &&
        !diagnosticBlock.Contains("20.0f*sizeScale", StringComparison.Ordinal) &&
        !diagnosticBlock.Contains("92.0f*sizeScale", StringComparison.Ordinal) &&
        !diagnosticBlock.Contains("+101.0f", StringComparison.Ordinal) &&
        !diagnosticBlock.Contains("+307.0f", StringComparison.Ordinal) &&
        !diagnosticBlock.Contains("+701.0f", StringComparison.Ordinal) &&
        !diagnosticBlock.Contains("uint3", StringComparison.Ordinal) &&
        !diagnosticBlock.Contains("float3", StringComparison.Ordinal) &&
        !diagnosticBlock.Contains("scenePosition.y", StringComparison.Ordinal) &&
        !diagnosticBlock.Contains("/64.0f", StringComparison.Ordinal),
        "水滴cell X bit G単独診断へHash演算、Seed、小・大セル、ベクトルbit、64px段階またはRGB同値出力を含めてはいけません。");
    True(
        !diagnosticBlock.Contains("HashNoise2D", StringComparison.Ordinal) &&
        !diagnosticBlock.Contains("sin(", StringComparison.Ordinal) &&
        !diagnosticBlock.Contains("fwidth", StringComparison.Ordinal) &&
        !diagnosticBlock.Contains("Texture.Sample", StringComparison.Ordinal) &&
        !diagnosticBlock.Contains("OriginalTexture", StringComparison.Ordinal) &&
        !diagnosticBlock.Contains("BlurredTexture", StringComparison.Ordinal) &&
        !diagnosticBlock.Contains("MaskTexture", StringComparison.Ordinal) &&
        !diagnosticBlock.Contains("outsideDropletLocalTimeSeconds", StringComparison.Ordinal) &&
        !diagnosticBlock.Contains("GetOutsideDropletLighting", StringComparison.Ordinal),
        "水滴cell X bit G単独診断へsin系Hash、導関数、画像サンプリング、落下時刻または照明を含めてはいけません。");

    var referenceCellXBits = BitConverter.SingleToUInt32Bits(-13.0f);
    var referenceBitValues = Enumerable.Range(0, 32)
        .Select(bitIndex => (referenceCellXBits >> bitIndex) & 1u)
        .ToArray();
    True(
        referenceBitValues.Contains(0u) && referenceBitValues.Contains(1u),
        "水滴cell X bit G単独診断のCPU参照は、セルX座標の32ビットから0と1を抽出できる必要があります。");

#if DEBUG
    Equal(
        21,
        (int)GlassWipeDebugView.OutsideDropletMediumOnlyCellXGreenBitWithoutSine,
        "Debug版の水滴cell X bit G単独表示は値21である必要があります。");
    Equal(
        "中セル単独・cell X bit G単独（診断）",
        GetDisplayName(GlassWipeDebugView.OutsideDropletMediumOnlyCellXGreenBitWithoutSine),
        "水滴cell X bit G単独診断の表示名が不正です。");
    var description = GetDisplayDescription(
        GlassWipeDebugView.OutsideDropletMediumOnlyCellXGreenBitWithoutSine);
    True(
        description.Contains("診断専用の対照表示", StringComparison.Ordinal) &&
        description.Contains("中水滴セルだけを44px基準で計算", StringComparison.Ordinal) &&
        description.Contains("セルX座標のfloatをasuintで32bit", StringComparison.Ordinal) &&
        description.Contains("Hash入力helper、Hash入力用dot、Seed加算を使いません", StringComparison.Ordinal) &&
        description.Contains("値20と同じビット位置", StringComparison.Ordinal) &&
        description.Contains("緑だけ", StringComparison.Ordinal) &&
        description.Contains("赤と青は0、alphaは1", StringComparison.Ordinal) &&
        description.Contains("Mix、小水滴・大水滴セル、uint3、画像サンプリングを使いません", StringComparison.Ordinal) &&
        description.Contains("4枚のスクリーンショット", StringComparison.Ordinal) &&
        description.Contains("必ず変化するとは限らない", StringComparison.Ordinal) &&
        description.Contains("画像差分", StringComparison.Ordinal) &&
        description.Contains("確認後は最終結果へ戻してください", StringComparison.Ordinal),
        "水滴cell X bit G単独診断は対照目的、セル入力、bit選択、G出力、除外要素、記録方法、非決定的な契機、判定方法、復帰手順を説明する必要があります。");
#else
    True(
        !Enum.IsDefined(typeof(GlassWipeDebugView), 21),
        "Release版へ水滴cell X bit G単独診断用Debug View値を公開してはいけません。");
    Equal(
        GlassWipeDebugView.Final,
        GlassWipeParameterSanitizer.SanitizeDebugView((GlassWipeDebugView)21),
        "Release版は水滴cell X bit G単独診断用値を最終結果へ補正する必要があります。");
#endif
}

static void VerifyOutsideDropletMediumOnlyInlineHashInputGreenBitDebugViewContract()
{
    var shaderSource = RemoveWhitespace(ShaderVerificationSource.ReadAllText(Path.Combine(
        FindRepositoryRoot(), "src", "YMM4GlassWipe", "Shaders", "GlassComposite.hlsl")));

    var diagnosticRangeStart = shaderSource.IndexOf(
        "if(debugView>=10.5f&&debugView<23.5f)",
        StringComparison.Ordinal);
    var previousDiagnosticBranchStart = shaderSource.IndexOf(
        "if(debugView>=20.5f&&debugView<21.5f)",
        Math.Max(diagnosticRangeStart, 0),
        StringComparison.Ordinal);
    var diagnosticBranchStart = shaderSource.IndexOf(
        "if(debugView>=21.5f&&debugView<22.5f)",
        Math.Max(previousDiagnosticBranchStart, 0),
        StringComparison.Ordinal);
    var mediumCellStart = shaderSource.IndexOf(
        "float2mediumOnlyCell=",
        Math.Max(diagnosticBranchStart, 0),
        StringComparison.Ordinal);
    var stripeIndexStart = shaderSource.IndexOf(
        "uintmediumOnlyInlineStripeIndex=",
        Math.Max(mediumCellStart, 0),
        StringComparison.Ordinal);
    var bitIndexStart = shaderSource.IndexOf(
        "uintmediumOnlyInlineBitIndex=",
        Math.Max(stripeIndexStart, 0),
        StringComparison.Ordinal);
    var inlineHashInputStart = shaderSource.IndexOf(
        "floatmediumOnlyInlineHashInput=dot(",
        Math.Max(bitIndexStart, 0),
        StringComparison.Ordinal);
    var inlineHashInputBitsStart = shaderSource.IndexOf(
        "uintmediumOnlyInlineHashInputBits=asuint(mediumOnlyInlineHashInput);",
        Math.Max(inlineHashInputStart, 0),
        StringComparison.Ordinal);
    var inlineHashInputBitValueStart = shaderSource.IndexOf(
        "floatmediumOnlyInlineHashInputBitValue=",
        Math.Max(inlineHashInputBitsStart, 0),
        StringComparison.Ordinal);
    var inlineHashInputReturnStart = shaderSource.IndexOf(
        "returnfloat4(0.0f,mediumOnlyInlineHashInputBitValue,0.0f,1.0f);",
        Math.Max(inlineHashInputBitValueStart, 0),
        StringComparison.Ordinal);
    var nextDiagnosticBranchStart = shaderSource.IndexOf(
        "if(debugView>=22.5f&&debugView<23.5f)",
        Math.Max(inlineHashInputReturnStart, 0),
        StringComparison.Ordinal);
    var sharedSmallCellStart = shaderSource.IndexOf(
        "float2smallCell=",
        Math.Max(nextDiagnosticBranchStart, 0),
        StringComparison.Ordinal);
    True(
        diagnosticRangeStart >= 0 &&
        previousDiagnosticBranchStart > diagnosticRangeStart &&
        diagnosticBranchStart > previousDiagnosticBranchStart &&
        mediumCellStart > diagnosticBranchStart &&
        stripeIndexStart > mediumCellStart &&
        bitIndexStart > stripeIndexStart &&
        inlineHashInputStart > bitIndexStart &&
        inlineHashInputBitsStart > inlineHashInputStart &&
        inlineHashInputBitValueStart > inlineHashInputBitsStart &&
        inlineHashInputReturnStart > inlineHashInputBitValueStart &&
        nextDiagnosticBranchStart > inlineHashInputReturnStart &&
        sharedSmallCellStart > nextDiagnosticBranchStart,
        "水滴直書きHash入力bit G単独診断が、値21と値23の間、かつ共通の小・中・大セル計算より前に正しい順序で見つかりません。");

    var diagnosticBlock = shaderSource[diagnosticBranchStart..nextDiagnosticBranchStart];
    foreach (var requiredExpression in new[]
             {
                 "float2mediumOnlyCell=floor(staticPatternPosition/max(44.0f*sizeScale,1.0f));",
                 "uintmediumOnlyInlineStripeIndex=(uint)floor(max(scenePosition.x,0.0f)/8.0f);",
                 "uintmediumOnlyInlineBitIndex=mediumOnlyInlineStripeIndex&31u;",
                 "floatmediumOnlyInlineHashInput=dot(mediumOnlyCell,float2(127.1f,311.7f))+((outsideDropletSeed+307.0f)+3.1f)*74.7f;",
                 "uintmediumOnlyInlineHashInputBits=asuint(mediumOnlyInlineHashInput);",
                 "floatmediumOnlyInlineHashInputBitValue=(float)((mediumOnlyInlineHashInputBits>>mediumOnlyInlineBitIndex)&1u);",
                 "returnfloat4(0.0f,mediumOnlyInlineHashInputBitValue,0.0f,1.0f);",
             })
    {
        True(
            diagnosticBlock.Contains(requiredExpression, StringComparison.Ordinal),
            "水滴直書きHash入力bit G単独診断契約が不足しています: " + requiredExpression);
    }

    Equal(
        0,
        diagnosticBlock.Split("GetOutsideDropletHashInputWithoutSine(", StringSplitOptions.None).Length - 1,
        "水滴直書きHash入力bit G単独診断ではHash入力helperを呼び出してはいけません。");
    Equal(
        1,
        diagnosticBlock.Split("dot(", StringSplitOptions.None).Length - 1,
        "水滴直書きHash入力bit G単独診断では値20相当のlattice dotを1回だけ計算する必要があります。");
    Equal(
        1,
        diagnosticBlock.Split("asuint(", StringSplitOptions.None).Length - 1,
        "水滴直書きHash入力bit G単独診断では直書きHash入力だけをasuintする必要があります。");
    Equal(
        1,
        diagnosticBlock.Split("returnfloat4(", StringSplitOptions.None).Length - 1,
        "水滴直書きHash入力bit G単独診断ではG単独の値だけを返す必要があります。");
    True(
        !diagnosticBlock.Contains("MixOutsideDropletHashInputWithoutSine(", StringComparison.Ordinal) &&
        !diagnosticBlock.Contains("smallCell", StringComparison.Ordinal) &&
        !diagnosticBlock.Contains("largeCell", StringComparison.Ordinal) &&
        !diagnosticBlock.Contains("20.0f*sizeScale", StringComparison.Ordinal) &&
        !diagnosticBlock.Contains("92.0f*sizeScale", StringComparison.Ordinal) &&
        !diagnosticBlock.Contains("+101.0f", StringComparison.Ordinal) &&
        !diagnosticBlock.Contains("+701.0f", StringComparison.Ordinal) &&
        !diagnosticBlock.Contains("uint3", StringComparison.Ordinal) &&
        !diagnosticBlock.Contains("float3", StringComparison.Ordinal) &&
        !diagnosticBlock.Contains("scenePosition.y", StringComparison.Ordinal) &&
        !diagnosticBlock.Contains("/64.0f", StringComparison.Ordinal) &&
        !diagnosticBlock.Contains("%3u", StringComparison.Ordinal),
        "水滴直書きHash入力bit G単独診断へMix、小・大セル、別Seed、ベクトルbit、64px段階またはRGB同値出力を含めてはいけません。");
    True(
        !diagnosticBlock.Contains("HashNoise2D", StringComparison.Ordinal) &&
        !diagnosticBlock.Contains("sin(", StringComparison.Ordinal) &&
        !diagnosticBlock.Contains("fwidth", StringComparison.Ordinal) &&
        !diagnosticBlock.Contains("Texture.Sample", StringComparison.Ordinal) &&
        !diagnosticBlock.Contains("OriginalTexture", StringComparison.Ordinal) &&
        !diagnosticBlock.Contains("BlurredTexture", StringComparison.Ordinal) &&
        !diagnosticBlock.Contains("MaskTexture", StringComparison.Ordinal) &&
        !diagnosticBlock.Contains("outsideDropletLocalTimeSeconds", StringComparison.Ordinal) &&
        !diagnosticBlock.Contains("GetOutsideDropletLighting", StringComparison.Ordinal),
        "水滴直書きHash入力bit G単独診断へsin系Hash、導関数、画像サンプリング、落下時刻または照明を含めてはいけません。");

    var referenceCell = new Vector2(12.0f, -7.0f);
    const float referenceSeed = 20.0f;
    var referenceInlineHashInput =
        Vector2.Dot(referenceCell, new Vector2(127.1f, 311.7f)) +
        ((referenceSeed + 307.0f) + 3.1f) * 74.7f;
    var referenceHelperBits = GetOutsideDropletHashInputBitsReference(
        referenceCell,
        referenceSeed + 307.0f,
        3.1f);
    Equal(
        referenceHelperBits,
        BitConverter.SingleToUInt32Bits(referenceInlineHashInput),
        "水滴直書きHash入力は値20のhelper入力と同じ評価順の32ビットになる必要があります。");
    var referenceBitValues = Enumerable.Range(0, 32)
        .Select(bitIndex => (referenceHelperBits >> bitIndex) & 1u)
        .ToArray();
    True(
        referenceBitValues.Contains(0u) && referenceBitValues.Contains(1u),
        "水滴直書きHash入力bit G単独診断のCPU参照は、Hash入力の32ビットから0と1を抽出できる必要があります。");

#if DEBUG
    Equal(
        22,
        (int)GlassWipeDebugView.OutsideDropletMediumOnlyInlineHashInputGreenBitWithoutSine,
        "Debug版の水滴直書きHash入力bit G単独表示は値22である必要があります。");
    Equal(
        "中セル単独・直書きHash入力bit G単独（診断）",
        GetDisplayName(GlassWipeDebugView.OutsideDropletMediumOnlyInlineHashInputGreenBitWithoutSine),
        "水滴直書きHash入力bit G単独診断の表示名が不正です。");
    var description = GetDisplayDescription(
        GlassWipeDebugView.OutsideDropletMediumOnlyInlineHashInputGreenBitWithoutSine);
    True(
        description.Contains("診断専用のhelper対照表示", StringComparison.Ordinal) &&
        description.Contains("値20と同じ中水滴セル、ビット位置、G単独出力", StringComparison.Ordinal) &&
        description.Contains("GetOutsideDropletHashInputWithoutSineは呼び出さず", StringComparison.Ordinal) &&
        description.Contains("同じHash入力式をこの分岐へ直接記述", StringComparison.Ordinal) &&
        description.Contains("赤と青は0、alphaは1", StringComparison.Ordinal) &&
        description.Contains("64px高の段階、RGBグレースケール、Mix、小水滴・大水滴セル、uint3、画像サンプリングを使いません", StringComparison.Ordinal) &&
        description.Contains("同一ビルドで値20と値22", StringComparison.Ordinal) &&
        description.Contains("4枚ずつ", StringComparison.Ordinal) &&
        description.Contains("必ず変化するとは限らない", StringComparison.Ordinal) &&
        description.Contains("画像差分", StringComparison.Ordinal) &&
        description.Contains("確認後は最終結果へ戻してください", StringComparison.Ordinal),
        "水滴直書きHash入力bit G単独診断は対照目的、直書き式、G出力、除外要素、同一ビルド比較、記録方法、非決定的な契機、判定方法、復帰手順を説明する必要があります。");
#else
    True(
        !Enum.IsDefined(typeof(GlassWipeDebugView), 22),
        "Release版へ水滴直書きHash入力bit G単独診断用Debug View値を公開してはいけません。");
    Equal(
        GlassWipeDebugView.Final,
        GlassWipeParameterSanitizer.SanitizeDebugView((GlassWipeDebugView)22),
        "Release版は水滴直書きHash入力bit G単独診断用値を最終結果へ補正する必要があります。");
#endif
}

static void VerifyOutsideDropletMediumOnlyCellYGreenBitDebugViewContract()
{
    var shaderSource = RemoveWhitespace(ShaderVerificationSource.ReadAllText(Path.Combine(
        FindRepositoryRoot(), "src", "YMM4GlassWipe", "Shaders", "GlassComposite.hlsl")));

    const string diagnosticRange = "if(debugView>=10.5f&&debugView<23.5f)";
    const string cellXBranch = "if(debugView>=20.5f&&debugView<21.5f)";
    const string inlineBranch = "if(debugView>=21.5f&&debugView<22.5f)";
    const string cellYBranch = "if(debugView>=22.5f&&debugView<23.5f)";
    var diagnosticRangeStart = shaderSource.IndexOf(diagnosticRange, StringComparison.Ordinal);
    var cellXBranchStart = shaderSource.IndexOf(cellXBranch, StringComparison.Ordinal);
    var inlineBranchStart = shaderSource.IndexOf(inlineBranch, StringComparison.Ordinal);
    var cellYBranchStart = shaderSource.IndexOf(cellYBranch, StringComparison.Ordinal);
    var nextDiagnosticBranchStart = shaderSource.IndexOf(
        "if(debugView<11.5f)", Math.Max(cellYBranchStart, 0), StringComparison.Ordinal);
    True(
        diagnosticRangeStart >= 0 &&
        cellXBranchStart > diagnosticRangeStart &&
        inlineBranchStart > cellXBranchStart &&
        cellYBranchStart > inlineBranchStart &&
        nextDiagnosticBranchStart > cellYBranchStart,
        "水滴cell Y bit G単独診断が、値22の後かつ共通の小・中・大セル計算より前に見つかりません。");

    var diagnosticBlock = shaderSource[cellYBranchStart..nextDiagnosticBranchStart];
    foreach (var requiredExpression in new[]
             {
                 "float2mediumOnlyCell=floor(staticPatternPosition/max(44.0f*sizeScale,1.0f));",
                 "uintmediumOnlyCellYStripeIndex=(uint)floor(max(scenePosition.x,0.0f)/8.0f);",
                 "uintmediumOnlyCellYBitIndex=mediumOnlyCellYStripeIndex&31u;",
                 "uintmediumOnlyCellYBits=asuint(mediumOnlyCell.y);",
                 "floatmediumOnlyCellYBitValue=(float)((mediumOnlyCellYBits>>mediumOnlyCellYBitIndex)&1u);",
                 "returnfloat4(0.0f,mediumOnlyCellYBitValue,0.0f,1.0f);",
             })
    {
        True(
            diagnosticBlock.Contains(requiredExpression, StringComparison.Ordinal),
            "水滴cell Y bit G単独診断契約が不足しています: " + requiredExpression);
    }

    Equal(
        shaderSource[cellXBranchStart..inlineBranchStart],
        diagnosticBlock
            .Replace(cellYBranch, cellXBranch, StringComparison.Ordinal)
            .Replace("mediumOnlyCellY", "mediumOnlyCellX", StringComparison.Ordinal)
            .Replace("asuint(mediumOnlyCell.y)", "asuint(mediumOnlyCell.x)", StringComparison.Ordinal),
        "値23は値21に対し分岐値、ローカル変数名、読み取るセル成分以外を変えてはいけません。");
    True(
        !diagnosticBlock.Contains("dot(", StringComparison.Ordinal) &&
        !diagnosticBlock.Contains("outsideDropletSeed", StringComparison.Ordinal) &&
        !diagnosticBlock.Contains("HashInput", StringComparison.Ordinal) &&
        !diagnosticBlock.Contains("Texture", StringComparison.Ordinal) &&
        !diagnosticBlock.Contains("asuint(mediumOnlyCell.x)", StringComparison.Ordinal),
        "水滴cell Y bit G単独診断へHash入力演算、Seed、画像参照またはセルXのbit抽出を含めてはいけません。");

    var referenceBitValues = new HashSet<uint>();
    foreach (var referenceCellY in new[] { -13.0f, 0.0f, 7.0f })
    {
        var referenceCellYBits = BitConverter.SingleToUInt32Bits(referenceCellY);
        foreach (var referenceCellX in new[] { -100.0f, 0.0f, 100.0f })
        {
            var referenceCell = new Vector2(referenceCellX, referenceCellY);
            for (var bitIndex = 0; bitIndex < 32; bitIndex++)
            {
                var expectedBit = (referenceCellYBits >> bitIndex) & 1u;
                foreach (var sceneX in new[] { bitIndex * 8.0f, bitIndex * 8.0f + 7.0f, bitIndex * 8.0f + 256.0f })
                {
                    var stripeIndex = (uint)MathF.Floor(MathF.Max(sceneX, 0.0f) / 8.0f);
                    var selectedBit = stripeIndex & 31u;
                    var actualBit = (BitConverter.SingleToUInt32Bits(referenceCell.Y) >> (int)selectedBit) & 1u;
                    Equal(
                        expectedBit, actualBit,
                        "水滴cell YのCPU参照はX成分に依存せず、8px幅・256px周期で同じY座標bitを選ぶ必要があります。");
                    referenceBitValues.Add(actualBit);
                }
            }
        }
    }
    True(
        referenceBitValues.SetEquals(new[] { 0u, 1u }),
        "水滴cell Y bit G単独診断のCPU参照は0と1の両方を抽出できる必要があります。");

#if DEBUG
    Equal(
        23,
        (int)GlassWipeDebugView.OutsideDropletMediumOnlyCellYGreenBitWithoutSine,
        "Debug版の水滴cell Y bit G単独表示は値23である必要があります。");
    Equal(
        "中セル単独・cell Y bit G単独（診断）",
        GetDisplayName(GlassWipeDebugView.OutsideDropletMediumOnlyCellYGreenBitWithoutSine),
        "水滴cell Y bit G単独診断の表示名が不正です。");
    var description = GetDisplayDescription(
        GlassWipeDebugView.OutsideDropletMediumOnlyCellYGreenBitWithoutSine);
    foreach (var requiredDescription in new[]
             {
                 "診断専用の対照表示",
                 "値21と同じ中水滴セルを44px基準で計算",
                 "セルY座標のfloatをasuintで32bit",
                 "Hash入力helper、Hash入力用dot、Seed加算を使いません",
                 "値21と同じビット位置",
                 "赤と青は0、alphaは1",
                 "Mix、小水滴・大水滴セル、uint3、画像サンプリングを使いません",
                 "4枚のスクリーンショット",
                 "必ず変化するとは限らない",
                 "画像差分",
                 "確認後は最終結果へ戻してください",
             })
    {
        True(
            description.Contains(requiredDescription, StringComparison.Ordinal),
            "水滴cell Y bit G単独診断の説明が不足しています: " + requiredDescription);
    }
#else
    True(
        !Enum.IsDefined(typeof(GlassWipeDebugView), 23),
        "Release版へ水滴cell Y bit G単独診断用Debug View値を公開してはいけません。");
    Equal(
        GlassWipeDebugView.Final,
        GlassWipeParameterSanitizer.SanitizeDebugView((GlassWipeDebugView)23),
        "Release版は水滴cell Y bit G単独診断用値を最終結果へ補正する必要があります。");
#endif
}

static void VerifyOutsideDropletCombinationStageReference()
{
    var cases = new[]
    {
        (Cell: new Vector2(0, 0), Seed: 121.0f),
        (Cell: new Vector2(12, -7), Seed: 327.0f),
        (Cell: new Vector2(-31, 44), Seed: 721.0f),
        (Cell: new Vector2(192, 108), Seed: 1138.0f),
    };
    var observedDifferentTerms = false;
    foreach (var (cell, seed) in cases)
    {
        var weightedTerms = Enumerable
            .Range(0, 5)
            .Select(termIndex => GetOutsideDropletWeightedHashTermReference(
                cell,
                seed,
                termIndex))
            .ToArray();
        observedDifferentTerms |= weightedTerms.Distinct().Count() > 1;

        var accumulated = weightedTerms[0];
        Equal(
            accumulated,
            GetOutsideDropletAccumulatedHashReference(cell, seed, 0),
            "第1項の累積値は係数1の乗算後値と一致する必要があります。");
        for (var stageIndex = 1; stageIndex < weightedTerms.Length; stageIndex++)
        {
            accumulated ^= weightedTerms[stageIndex];
            Equal(
                accumulated,
                GetOutsideDropletAccumulatedHashReference(cell, seed, stageIndex),
                $"第{stageIndex + 1}項までのXOR累積値が不正です。");
        }

        Equal(
            GetOutsideDropletPreAvalancheHashReference(cell, seed),
            accumulated,
            "Debug19の第5帯はDebug17相当の最終avalanche前ビットと一致する必要があります。");
    }

    True(
        observedDifferentTerms,
        "固定セル・Seedの数値参照では、5個の乗算後項がすべて同一になってはいけません。");
}

static uint GetOutsideDropletWeightedHashTermReference(
    Vector2 cell,
    float seed,
    int termIndex)
{
    var safeTermIndex = Math.Clamp(termIndex, 0, 4);
    var offset = safeTermIndex switch
    {
        0 => 3.1f,
        1 => 11.7f,
        2 => 29.3f,
        3 => 47.9f,
        _ => 61.1f,
    };
    var multiplier = safeTermIndex switch
    {
        0 => 1u,
        1 => 0x9e3779b9u,
        2 => 0x85ebca6bu,
        3 => 0xc2b2ae35u,
        _ => 0x27d4eb2fu,
    };
    return unchecked(
        MixOutsideDropletHashInputWithoutSineReference(cell, seed, offset) *
        multiplier);
}

static uint GetOutsideDropletAccumulatedHashReference(
    Vector2 cell,
    float seed,
    int stageIndex)
{
    var safeStageIndex = Math.Clamp(stageIndex, 0, 4);
    var accumulated = GetOutsideDropletWeightedHashTermReference(
        cell,
        seed,
        0);
    for (var termIndex = 1; termIndex <= safeStageIndex; termIndex++)
    {
        accumulated ^= GetOutsideDropletWeightedHashTermReference(
            cell,
            seed,
            termIndex);
    }

    return accumulated;
}

static uint GetOutsideDropletPreAvalancheHashReference(
    Vector2 cell,
    float seed)
{
    var fingerprint = MixOutsideDropletHashInputWithoutSineReference(
        cell,
        seed,
        3.1f);
    fingerprint ^= unchecked(
        MixOutsideDropletHashInputWithoutSineReference(cell, seed, 11.7f) *
        0x9e3779b9u);
    fingerprint ^= unchecked(
        MixOutsideDropletHashInputWithoutSineReference(cell, seed, 29.3f) *
        0x85ebca6bu);
    fingerprint ^= unchecked(
        MixOutsideDropletHashInputWithoutSineReference(cell, seed, 47.9f) *
        0xc2b2ae35u);
    fingerprint ^= unchecked(
        MixOutsideDropletHashInputWithoutSineReference(cell, seed, 61.1f) *
        0x27d4eb2fu);
    return fingerprint;
}

static uint MixOutsideDropletHashInputWithoutSineReference(
    Vector2 cell,
    float seed,
    float offset)
{
    var mixed = GetOutsideDropletHashInputBitsReference(cell, seed, offset);
    mixed ^= mixed >> 16;
    mixed = unchecked(mixed * 0x7feb352du);
    mixed ^= mixed >> 15;
    mixed = unchecked(mixed * 0x846ca68bu);
    mixed ^= mixed >> 16;
    return mixed;
}

static uint GetOutsideDropletHashInputBitsReference(
    Vector2 cell,
    float seed,
    float offset)
{
    var hashInput =
        Vector2.Dot(cell, new Vector2(127.1f, 311.7f)) +
        (seed + offset) * 74.7f;
    return BitConverter.SingleToUInt32Bits(hashInput);
}

static void VerifyPreFogParameterIsolation()
{
    var baseline = default(GlassWipeParameters);
    var fogOnly = baseline with
    {
        FogAmount = 0.7f,
        Blur = 30,
        FogTintRed = 1f,
        FogTintGreen = 0.5f,
        FogTintBlue = 0.2f,
        TintMix = 0.8f,
        WipeResidue = 0.3f,
        WipeVariation = 0.4f,
        FogNoise = 0.6f,
        NoiseSeed = 17f,
        OutsideDropletLocalTimeSeconds = 2f,
    };
    True(baseline.HasSamePreFogNonTemporalValues(fogOnly),
        "曇り専用値と時刻だけの変更で水滴前段を再適用してはいけません。");
    True(!baseline.HasSameCompositeNonTemporalValues(fogOnly),
        "曇り専用値の変更は最終合成へ反映する必要があります。");
    foreach (var property in typeof(GlassWipeParameters).GetProperties()
        .Where(property => property.PropertyType == typeof(float)))
    {
        // 実際に反映先が異なる値を個別に変え、除外し過ぎと除外漏れを確認する。
        var fogNames = new[] { "FogAmount", "Blur", "FogTintRed", "FogTintGreen",
            "FogTintBlue", "TintMix", "WipeResidue", "WipeVariation", "FogNoise",
            "NoiseSeed", "OutsideDropletLocalTimeSeconds" };
        object changed = baseline;
        property.SetValue(changed, 1f);
        True(baseline.HasSamePreFogNonTemporalValues((GlassWipeParameters)changed) ==
            fogNames.Contains(property.Name),
            $"水滴前段の変更検出が不正です: {property.Name}");
    }
    True(!baseline.HasSamePreFogNonTemporalValues(fogOnly with { RegionShape = GlassWipeRegionShape.Quad }),
        "領域形状の変更を省略してはいけません。");
    True(!baseline.HasSamePreFogNonTemporalValues(fogOnly with { DebugView = GlassWipeDebugView.Blurred }),
        "表示切替の変更を省略してはいけません。");
    var quad = GlassWipeQuad.FrontWindshield;
    True(quad.TryCreateMapping(out var mapping), "検証用四角形の変換が必要です。");
    True(!baseline.HasSamePreFogNonTemporalValues(fogOnly with { Quad = quad }),
        "四角形の頂点変更を省略してはいけません。");
    True(!baseline.HasSamePreFogNonTemporalValues(fogOnly with { QuadMapping = mapping }),
        "四角形の変換変更を省略してはいけません。");
}

static void VerifyBlurCompositeParameterIsolation()
{
    var baseline = default(GlassWipeParameters) with
    {
        FogAmount = 0.7f,
        Blur = 15,
        OutsideDropletLocalTimeSeconds = 1,
    };
    var blurAndTime = baseline with { Blur = 30, OutsideDropletLocalTimeSeconds = 2 };
    True(!baseline.HasSameNonTemporalValues(blurAndTime),
        "従来の非時刻比較では、ぼかし量の差を維持する必要があります。");
    True(baseline.HasSameCompositeNonTemporalValues(blurAndTime),
        "ぼかし量と時刻だけの差で合成定数を再適用してはいけません。");
    foreach (var (name, changed) in new[]
    {
        ("曇り量", blurAndTime with { FogAmount = 0.4f }),
        ("曇り色", blurAndTime with { FogTintRed = 1f }),
        ("色の混合", blurAndTime with { TintMix = 1f }),
        ("領域", blurAndTime with { RegionWidth = 1f }),
        ("領域の輪郭", blurAndTime with { RegionFeather = 1f }),
        ("拭き残し", blurAndTime with { WipeResidue = 0.2f }),
        ("曇りノイズ", blurAndTime with { FogNoise = 0.2f }),
        ("水滴量", blurAndTime with { OutsideDropletAmount = 0.5f }),
        ("水滴の濃さ", blurAndTime with { OutsideDropletStrength = 0.5f }),
        ("水滴Seed", blurAndTime with { OutsideDropletSeed = 3f }),
        ("雨の開始", blurAndTime with { OutsideDropletRainEnabled = 1f }),
        ("落下", blurAndTime with { OutsideDropletFallEnabled = 1f }),
        ("合体", blurAndTime with { OutsideDropletMergeEnabled = 1f }),
        ("頻度", blurAndTime with { OutsideDropletFallFrequency = 2f }),
        ("表示", blurAndTime with { DebugView = GlassWipeDebugView.Blurred }),
    })
    {
        True(!baseline.HasSameCompositeNonTemporalValues(changed),
            $"{name}の差をぼかし量とともに省略してはいけません。");
    }
}
static void VerifyOutsideDropletTemporalUpdateOptimization()
{
    var firstFrame = default(GlassWipeParameters) with
    {
        OutsideDropletLocalTimeSeconds = 1,
    };
    var nextFrame = firstFrame with
    {
        OutsideDropletLocalTimeSeconds = 2,
    };

    True(
        firstFrame.HasSameNonTemporalValues(nextFrame),
        "落下時刻の差は時刻単独更新で扱う必要があります。");
    True(
        !firstFrame.HasSameNonTemporalValues(nextFrame with
        {
            OutsideDropletFallEnabled = 1,
        }),
        "落下ON/OFFの差では全定数を再適用する必要があります。");
    True(
        !firstFrame.HasSameNonTemporalValues(nextFrame with
        {
            OutsideDropletAmount = 0.5f,
        }),
        "水滴設定の差では全定数を再適用する必要があります。");

    NearlyEqual(
        0,
        GlassWipeParameters.ResolveOutsideDropletLocalTimeSeconds(
            false,
            600,
            60),
        "落下OFFでは時刻を固定する必要があります。",
        0.000001f);
    NearlyEqual(
        10,
        GlassWipeParameters.ResolveOutsideDropletLocalTimeSeconds(
            true,
            600,
            60),
        "落下ONではローカル時刻をフレームから求める必要があります。",
        0.000001f);

    var repositoryRoot = FindRepositoryRoot();
    var effectSource = RemoveWhitespace(File.ReadAllText(
        Path.Combine(
            repositoryRoot,
            "src",
            "YMM4GlassWipe",
            "GlassCompositeCustomEffect.cs")));
    var resourcesSource = RemoveWhitespace(File.ReadAllText(
        Path.Combine(
            repositoryRoot,
            "src",
            "YMM4GlassWipe",
            "GlassWipeEffectResources.cs")));

    True(
        effectSource.Contains(
            "publicvoidApplyOutsideDropletLocalTime(floatlocalTimeSeconds){SetValue((int)EffectImpl.Properties.OutsideDropletLocalTimeSeconds,localTimeSeconds);}",
            StringComparison.Ordinal),
        "落下中の時刻差はOutsideDropletLocalTimeSecondsだけを変更する必要があります。");
    var fullBranchIndex = resourcesSource.IndexOf(
        "if(!_hasParameters||!_currentParameters.HasSameCompositeNonTemporalValues(parameters)",
        StringComparison.Ordinal);
    var fullTraceIndex = resourcesSource.IndexOf(
        "PreviewTraceEvent.ResourceFullApply",
        StringComparison.Ordinal);
    var fullUpdateIndex = resourcesSource.IndexOf(
        "_compositeEffect.ApplyParameters(compositeParameters,inputLeft,inputTop,inputWidth,inputHeight);",
        StringComparison.Ordinal);
    var timeBranchIndex = resourcesSource.IndexOf(
        "elseif(_currentParameters.OutsideDropletLocalTimeSeconds!=parameters.OutsideDropletLocalTimeSeconds){",
        StringComparison.Ordinal);
    var timeTraceIndex = resourcesSource.IndexOf(
        "PreviewTraceEvent.ResourceTimeApply",
        StringComparison.Ordinal);
    var timeUpdateIndex = resourcesSource.IndexOf(
        "_compositeEffect.ApplyOutsideDropletLocalTime(parameters.OutsideDropletLocalTimeSeconds);",
        StringComparison.Ordinal);
    var noApplyTraceIndex = resourcesSource.IndexOf(
        "PreviewTraceEvent.ResourceNoParameterApply",
        StringComparison.Ordinal);
    var cacheUpdateIndex = resourcesSource.IndexOf(
        "_currentParameters=parameters;",
        timeUpdateIndex,
        StringComparison.Ordinal);

    True(fullBranchIndex >= 0, "時刻以外の差では全定数を再適用する条件が必要です。");
    True(
        fullTraceIndex > fullBranchIndex &&
        fullUpdateIndex > fullTraceIndex,
        "全定数更新分岐ではResourceFullApplyを記録してからGPU定数を更新する必要があります。");
    True(
        timeBranchIndex > fullUpdateIndex &&
        timeTraceIndex > timeBranchIndex &&
        timeUpdateIndex > timeTraceIndex,
        "時刻単独更新分岐ではResourceTimeApplyを記録してから時刻を更新する必要があります。");
    True(
        noApplyTraceIndex > timeUpdateIndex,
        "定数を更新しない分岐ではResourceNoParameterApplyを記録する必要があります。");
    True(
        cacheUpdateIndex > noApplyTraceIndex,
        "GPU定数更新が成功した後でキャッシュを更新する必要があります。");
}

static void VerifyCompositeOutputCacheDiagnosticContract()
{
    var repositoryRoot = FindRepositoryRoot();
    var effectSource = RemoveWhitespace(File.ReadAllText(Path.Combine(
        repositoryRoot,
        "src",
        "YMM4GlassWipe",
        "GlassCompositeCustomEffect.cs")));
    var resourcesSource = RemoveWhitespace(File.ReadAllText(Path.Combine(
        repositoryRoot,
        "src",
        "YMM4GlassWipe",
        "GlassWipeEffectResources.cs")));
    var traceSource = RemoveWhitespace(File.ReadAllText(Path.Combine(
        repositoryRoot,
        "src",
        "YMM4GlassWipe",
        "PreviewUpdateTrace.cs")));

    True(
        effectSource.Contains(
            "internalboolGetCachedForDebug()=>GetBoolValue((int)Property.Cached);",
            StringComparison.Ordinal) &&
        effectSource.Contains(
            "internalvoidSetCachedForDebug(boolcached)=>SetValue((int)Property.Cached,cached);",
            StringComparison.Ordinal),
        "診断A/BはDirect2DのCached値を設定後に読み戻せる必要があります。");
    True(
        resourcesSource.Contains(
            "parameters.DebugView==GlassWipeDebugView.OutsideDropletCoverage",
            StringComparison.Ordinal),
        "描画出力キャッシュA/Bは外側水滴の被覆診断だけを対象にする必要があります。");
    True(
        resourcesSource.Contains(
            "_diagnosticOriginalCompositeCached??=cachedBefore;",
            StringComparison.Ordinal) &&
        resourcesSource.Contains(
            "_compositeEffect.SetCachedForDebug(true);",
            StringComparison.Ordinal) &&
        resourcesSource.Contains(
            "_compositeEffect.SetCachedForDebug(originalCached);",
            StringComparison.Ordinal),
        "診断A/Bは元のCached値を保存し、診断終了時に復元する必要があります。");
    var readbackFailureIndex = resourcesSource.IndexOf(
        "if(!cachedAfter){PreviewUpdateTrace.Record(PreviewTraceEvent.ResourceCompositeCacheFailed",
        StringComparison.Ordinal);
    var cacheModeActivationIndex = resourcesSource.IndexOf(
        "_diagnosticCompositeCacheModeActive=true;",
        StringComparison.Ordinal);
    True(
        readbackFailureIndex >= 0 &&
        cacheModeActivationIndex > readbackFailureIndex,
        "Cached読戻しが成功した後だけ診断状態を有効にする必要があります。");
    True(
        resourcesSource.Contains(
            "(originalCached?1:0)|(cachedBefore?2:0)|(cachedAfter?4:0)",
            StringComparison.Ordinal) &&
        resourcesSource.Contains(
            "CompositeCacheApplyExceptionDetail=-1",
            StringComparison.Ordinal) &&
        resourcesSource.Contains(
            "CompositeCacheRestoreExceptionDetail=-2",
            StringComparison.Ordinal),
        "Cached診断ログは元値、操作前、読戻し後、例外方向を区別する必要があります。");
    True(
        resourcesSource.Contains(
            "PreviewTraceEvent.ResourceCompositeCacheApply",
            StringComparison.Ordinal) &&
        resourcesSource.Contains(
            "PreviewTraceEvent.ResourceCompositeCacheRestore",
            StringComparison.Ordinal) &&
        resourcesSource.Contains(
            "PreviewTraceEvent.ResourceCompositeCacheFailed",
            StringComparison.Ordinal) &&
        traceSource.Contains("ResourceCompositeCacheApply", StringComparison.Ordinal) &&
        traceSource.Contains("ResourceCompositeCacheRestore", StringComparison.Ordinal) &&
        traceSource.Contains("ResourceCompositeCacheFailed", StringComparison.Ordinal),
        "Cached値の適用、復元、失敗を診断ログへ記録する必要があります。");

    var pluginAssemblyPath = typeof(GlassCompositeConstants).Assembly.Location;
    var hasGetCachedMethod = HasMethodDefinition(
        pluginAssemblyPath,
        "YMM4GlassWipe",
        "GlassCompositeCustomEffect",
        "GetCachedForDebug");
    var hasSetCachedMethod = HasMethodDefinition(
        pluginAssemblyPath,
        "YMM4GlassWipe",
        "GlassCompositeCustomEffect",
        "SetCachedForDebug");
    var hasUpdateCacheMethod = HasMethodDefinition(
        pluginAssemblyPath,
        "YMM4GlassWipe",
        "GlassWipeEffectResources",
        "UpdateCompositeOutputCacheForDebug");
#if DEBUG
    True(
        hasGetCachedMethod &&
        hasSetCachedMethod &&
        hasUpdateCacheMethod,
        "Debug版には描画出力キャッシュA/B診断が必要です。");
    True(
        Enum.IsDefined(
            typeof(PreviewTraceEvent),
            "ResourceCompositeCacheApply") &&
        Enum.IsDefined(
            typeof(PreviewTraceEvent),
            "ResourceCompositeCacheRestore") &&
        Enum.IsDefined(
            typeof(PreviewTraceEvent),
            "ResourceCompositeCacheFailed"),
        "Debug版には描画出力キャッシュの診断イベントが必要です。");
#else
    True(
        !hasGetCachedMethod &&
        !hasSetCachedMethod &&
        !hasUpdateCacheMethod,
        "Release版へ描画出力キャッシュA/B診断を含めてはいけません。");
    True(
        !Enum.IsDefined(
            typeof(PreviewTraceEvent),
            "ResourceCompositeCacheApply") &&
        !Enum.IsDefined(
            typeof(PreviewTraceEvent),
            "ResourceCompositeCacheRestore") &&
        !Enum.IsDefined(
            typeof(PreviewTraceEvent),
            "ResourceCompositeCacheFailed"),
        "Release版へ描画出力キャッシュの診断イベントを含めてはいけません。");
#endif
}

static void VerifyCompositeMaskInputBypassDiagnosticContract()
{
    var repositoryRoot = FindRepositoryRoot();
    var resourcesSource = RemoveWhitespace(File.ReadAllText(Path.Combine(
        repositoryRoot,
        "src",
        "YMM4GlassWipe",
        "GlassWipeEffectResources.cs")));
    var traceSource = RemoveWhitespace(File.ReadAllText(Path.Combine(
        repositoryRoot,
        "src",
        "YMM4GlassWipe",
        "PreviewUpdateTrace.cs")));

    True(
        resourcesSource.Contains(
            "UpdateCompositeMaskInputBypassForDebug(parameters);UpdateCompositeOutputCacheForDebug(parameters);",
            StringComparison.Ordinal),
        "マスク入力バイパスはキャッシュ診断と分離し、先に状態を更新する必要があります。");
    var methodIndex = resourcesSource.IndexOf(
        "privatevoidUpdateCompositeMaskInputBypassForDebug",
        StringComparison.Ordinal);
    var debugViewIndex = resourcesSource.IndexOf(
        "parameters.DebugViewisGlassWipeDebugView.OutsideDropletCellHashWithoutSineorGlassWipeDebugView.OutsideDropletFinalHashBitsWithoutSineorGlassWipeDebugView.OutsideDropletMixedHashBitsWithoutSineorGlassWipeDebugView.OutsideDropletPreAvalancheHashBitsWithoutSineorGlassWipeDebugView.OutsideDropletWeightedHashBitsWithoutSineorGlassWipeDebugView.OutsideDropletAccumulatedHashBitsWithoutSineorGlassWipeDebugView.OutsideDropletMediumOnlyHashInputGreenBitWithoutSineorGlassWipeDebugView.OutsideDropletMediumOnlyCellXGreenBitWithoutSineorGlassWipeDebugView.OutsideDropletMediumOnlyInlineHashInputGreenBitWithoutSineorGlassWipeDebugView.OutsideDropletMediumOnlyCellYGreenBitWithoutSine",
        methodIndex,
        StringComparison.Ordinal);
    var applyIndex = resourcesSource.IndexOf(
        "_compositeEffect.SetInput(2,diagnosticMask,true);",
        methodIndex,
        StringComparison.Ordinal);
    var activateIndex = resourcesSource.IndexOf(
        "_diagnosticCompositeMaskInputBypassActive=true;",
        applyIndex,
        StringComparison.Ordinal);
    var restoreIndex = resourcesSource.IndexOf(
        "if(!TryRestoreCompositeMaskInputForDebug())",
        activateIndex,
        StringComparison.Ordinal);
    var deactivateIndex = resourcesSource.IndexOf(
        "_diagnosticCompositeMaskInputBypassActive=false;",
        restoreIndex,
        StringComparison.Ordinal);
    True(
        methodIndex >= 0 &&
        debugViewIndex > methodIndex &&
        applyIndex > debugViewIndex &&
        activateIndex > applyIndex,
        "Debug11/14/16/17/18/19/20/21/22/23で入力2を固定透明マスクへ差し替えた後だけ、バイパス状態を有効にする必要があります。");
    True(
        restoreIndex > activateIndex &&
        deactivateIndex > restoreIndex,
        "Debug11/14/16/17/18/19/20/21/22/23終了時に入力2へマスクを再接続してから、バイパス状態を解除する必要があります。");
    True(
        resourcesSource.Contains(
            "TryRestoreCompositeMaskInputForDebug(){try{_compositeEffect.SetInput(2,_maskRenderer.Image,true);returntrue;}",
            StringComparison.Ordinal),
        "復元ヘルパーは入力2へマスクを再接続し、成功した場合だけtrueを返す必要があります。");
    True(
        !resourcesSource.Contains(
            "_compositeEffect.SetInput(2,input,true);",
            StringComparison.Ordinal) &&
        !resourcesSource.Contains(
            "_compositeEffect.SetInput(2,_input,true);",
            StringComparison.Ordinal),
        "固定透明マスク診断中に入力2を現在入力へ追従させてはいけません。");
    True(
        resourcesSource.Contains(
            "CompositeMaskInputBypassApplyExceptionDetail=-1",
            StringComparison.Ordinal) &&
        resourcesSource.Contains(
            "CompositeMaskInputBypassRestoreExceptionDetail=-2",
            StringComparison.Ordinal) &&
        resourcesSource.Contains(
            "CompositeMaskInputBypassApplyRecoveryExceptionDetail=-4",
            StringComparison.Ordinal),
        "固定透明マスク入力の適用、復元、復旧失敗を区別する必要があります。");
    True(
        resourcesSource.Contains(
            "varrestored=TryRestoreCompositeMaskInputForDebug();_diagnosticCompositeMaskInputBypassActive=!restored;",
            StringComparison.Ordinal) &&
        resourcesSource.Contains(
            "if(!TryRestoreCompositeMaskInputForDebug()){PreviewUpdateTrace.Record(PreviewTraceEvent.ResourceCompositeMaskInputBypassFailed",
            StringComparison.Ordinal),
        "適用後の復旧に失敗した場合は、再接続待ちを保持して通常表示で再試行する必要があります。");
    True(
        resourcesSource.Contains(
            "GetImageLocalBoundsForDebug(diagnosticMask,outvardiagnosticMaskLeft,outvardiagnosticMaskTop,outvardiagnosticMaskWidth,outvardiagnosticMaskHeight);",
            StringComparison.Ordinal) &&
        resourcesSource.Contains(
            "inputLeft:diagnosticMaskLeft,inputTop:diagnosticMaskTop,inputWidth:diagnosticMaskWidth,inputHeight:diagnosticMaskHeight,inputIdentityId:PreviewUpdateTrace.GetInputIdentityId(diagnosticMask)",
            StringComparison.Ordinal) &&
        resourcesSource.Contains(
            "GetImageLocalBoundsForDebug(_maskRenderer.Image,outvarmaskLeft,outvarmaskTop,outvarmaskWidth,outvarmaskHeight);",
            StringComparison.Ordinal) &&
        resourcesSource.Contains(
            "inputLeft:maskLeft,inputTop:maskTop,inputWidth:maskWidth,inputHeight:maskHeight,inputIdentityId:PreviewUpdateTrace.GetInputIdentityId(_maskRenderer.Image)",
            StringComparison.Ordinal),
        "A/Bの適用と復元ではslot2の入力識別子と実際の矩形を記録する必要があります。");
    True(
        resourcesSource.Contains(
            "privateID2D1Bitmap1CreateFixedTransparentMaskBitmapForDebug()",
            StringComparison.Ordinal) &&
        resourcesSource.Contains(
            "newbyte[checked(rowPitch*WipeMaskRenderer.MaskSize)]",
            StringComparison.Ordinal) &&
        resourcesSource.Contains(
            "Vortice.DXGI.Format.B8G8R8A8_UNorm",
            StringComparison.Ordinal) &&
        resourcesSource.Contains(
            "Vortice.DCommon.AlphaMode.Premultiplied",
            StringComparison.Ordinal) &&
        resourcesSource.Contains(
            "BitmapOptions.None",
            StringComparison.Ordinal) &&
        resourcesSource.Contains(
            "newVortice.Mathematics.SizeI(WipeMaskRenderer.MaskSize,WipeMaskRenderer.MaskSize)",
            StringComparison.Ordinal),
        "診断用入力は通常マスクと同じ1024x1024の透明・非Target Bitmapである必要があります。");
    var disposeMethodIndex = resourcesSource.IndexOf(
        "publicvoidDispose()",
        StringComparison.Ordinal);
    var detachMaskIndex = resourcesSource.IndexOf(
        "_compositeEffect.SetInput(2,null,true)",
        disposeMethodIndex,
        StringComparison.Ordinal);
    var disposeDiagnosticMaskIndex = resourcesSource.IndexOf(
        "TryRelease(()=>_diagnosticFixedTransparentMaskBitmap?.Dispose());",
        detachMaskIndex,
        StringComparison.Ordinal);
    var disposeCompositeIndex = resourcesSource.IndexOf(
        "TryRelease(_compositeEffect.Dispose);",
        disposeDiagnosticMaskIndex,
        StringComparison.Ordinal);
    True(
        disposeMethodIndex >= 0 &&
        detachMaskIndex > disposeMethodIndex &&
        disposeDiagnosticMaskIndex > detachMaskIndex &&
        disposeCompositeIndex > disposeDiagnosticMaskIndex,
        "入力2を解除した後、Composite Effectより先に診断用Bitmapを解放する必要があります。");
    True(
        resourcesSource.Contains(
            "PreviewTraceEvent.ResourceCompositeMaskInputBypassApply",
            StringComparison.Ordinal) &&
        resourcesSource.Contains(
            "PreviewTraceEvent.ResourceCompositeMaskInputBypassRestore",
            StringComparison.Ordinal) &&
        resourcesSource.Contains(
            "PreviewTraceEvent.ResourceCompositeMaskInputBypassFailed",
            StringComparison.Ordinal) &&
        traceSource.Contains(
            "ResourceCompositeMaskInputBypassApply",
            StringComparison.Ordinal) &&
        traceSource.Contains(
            "ResourceCompositeMaskInputBypassRestore",
            StringComparison.Ordinal) &&
        traceSource.Contains(
            "ResourceCompositeMaskInputBypassFailed",
            StringComparison.Ordinal),
        "マスク入力バイパスの適用、復元、失敗を診断ログへ記録する必要があります。");

    var pluginAssemblyPath = typeof(GlassCompositeConstants).Assembly.Location;
    var hasUpdateMethod = HasMethodDefinition(
        pluginAssemblyPath,
        "YMM4GlassWipe",
        "GlassWipeEffectResources",
        "UpdateCompositeMaskInputBypassForDebug");
    var hasCreateBitmapMethod = HasMethodDefinition(
        pluginAssemblyPath,
        "YMM4GlassWipe",
        "GlassWipeEffectResources",
        "CreateFixedTransparentMaskBitmapForDebug");
    var hasDiagnosticBitmapField = HasFieldDefinition(
        pluginAssemblyPath,
        "YMM4GlassWipe",
        "GlassWipeEffectResources",
        "_diagnosticFixedTransparentMaskBitmap");
#if DEBUG
    True(hasUpdateMethod, "Debug版にはマスク入力バイパスA/B診断が必要です。");
    True(
        hasCreateBitmapMethod && hasDiagnosticBitmapField,
        "Debug版には固定透明マスクBitmapの生成ヘルパーと所有フィールドが必要です。");
    True(
        Enum.IsDefined(
            typeof(PreviewTraceEvent),
            "ResourceCompositeMaskInputBypassApply") &&
        Enum.IsDefined(
            typeof(PreviewTraceEvent),
            "ResourceCompositeMaskInputBypassRestore") &&
        Enum.IsDefined(
            typeof(PreviewTraceEvent),
            "ResourceCompositeMaskInputBypassFailed"),
        "Debug版にはマスク入力バイパスの診断イベントが必要です。");
#else
    True(!hasUpdateMethod, "Release版へマスク入力バイパスA/B診断を含めてはいけません。");
    True(
        !hasCreateBitmapMethod && !hasDiagnosticBitmapField,
        "Release版へ固定透明マスクBitmapの生成ヘルパーや所有フィールドを含めてはいけません。");
    True(
        !Enum.IsDefined(
            typeof(PreviewTraceEvent),
            "ResourceCompositeMaskInputBypassApply") &&
        !Enum.IsDefined(
            typeof(PreviewTraceEvent),
            "ResourceCompositeMaskInputBypassRestore") &&
        !Enum.IsDefined(
            typeof(PreviewTraceEvent),
            "ResourceCompositeMaskInputBypassFailed"),
        "Release版へマスク入力バイパスの診断イベントを含めてはいけません。");
#endif
}

static void VerifyRenderCallbackTraceContract()
{
    var repositoryRoot = FindRepositoryRoot();
    var effectSource = File.ReadAllText(Path.Combine(
        repositoryRoot,
        "src",
        "YMM4GlassWipe",
        "GlassCompositeCustomEffect.cs"));
    var traceSource = File.ReadAllText(Path.Combine(
        repositoryRoot,
        "src",
        "YMM4GlassWipe",
        "RenderCallbackTrace.cs"));
    var processorSource = File.ReadAllText(Path.Combine(
        repositoryRoot,
        "src",
        "YMM4GlassWipe",
        "GlassWipeVideoEffectProcessor.cs"));
    var compactEffectSource = RemoveWhitespace(effectSource);
    var compactProcessorSource = RemoveWhitespace(processorSource);

    const string spdxHeader = "// SPDX-License-Identifier: MPL-2.0";
    var traceImplementationSource = traceSource.StartsWith(
        spdxHeader,
        StringComparison.Ordinal)
        ? traceSource[spdxHeader.Length..]
        : traceSource;
    True(
        traceImplementationSource.TrimStart().StartsWith(
            "#if DEBUG",
            StringComparison.Ordinal) &&
        traceImplementationSource.TrimEnd().EndsWith(
            "#endif",
            StringComparison.Ordinal),
        "SPDXヘッダーを除く描画コールバックトレース全体をDebug限定にする必要があります。");
#if DEBUG
    True(
        typeof(GlassCompositeConstants).Assembly.GetType(
            "YMM4GlassWipe.RenderCallbackTrace") is not null,
        "Debugビルドには描画コールバックトレースが必要です。");
#else
    True(
        typeof(GlassCompositeConstants).Assembly.GetType(
            "YMM4GlassWipe.RenderCallbackTrace") is null,
        "Releaseビルドに描画コールバックトレースを含めてはいけません。");
#endif

    var constantFields = typeof(GlassCompositeConstants)
        .GetFields(BindingFlags.Instance | BindingFlags.Public);
    Equal(60, constantFields.Length, "描画時に記録する定数バッファのfloat数");
    True(
        traceSource.Contains(
            "MemoryMarshal.Cast<GlassCompositeConstants, float>",
            StringComparison.Ordinal) &&
        traceSource.Contains(
            "BitConverter.SingleToInt32Bits(value)",
            StringComparison.Ordinal) &&
        traceSource.Contains(".ToString(\"X8\"", StringComparison.Ordinal),
        "全定数をIEEE 754の生32bitとして記録する必要があります。");
    True(
        traceSource.Contains("effect_instance_id", StringComparison.Ordinal) &&
        traceSource.Contains("callback_sequence", StringComparison.Ordinal) &&
        traceSource.Contains("draw_information_available", StringComparison.Ordinal),
        "描画個体とコールバック順序を記録する必要があります。");
    foreach (var requiredColumn in new[]
    {
        "input_count",
        "input_unrecorded_count",
        "opaque_count",
        "opaque_unrecorded_count",
        "input0_left",
        "input2_bottom",
        "opaque0_left",
        "opaque2_bottom",
        "output_left",
        "output_opaque_bottom",
    })
    {
        True(
            traceSource.Contains(requiredColumn, StringComparison.Ordinal),
            $"描画範囲トレース列 {requiredColumn} が必要です。");
    }

    True(
        traceSource.Contains(
            "RenderCallbackTraceEvent.RenderConstants",
            StringComparison.Ordinal) &&
        traceSource.Contains(
            "RenderCallbackTraceEvent.MapInputRectsToOutputRect",
            StringComparison.Ordinal) &&
        traceSource.Contains(
            "RenderCallbackTraceEvent.MapOutputRectToInputRects",
            StringComparison.Ordinal),
        "定数送信と両方向の範囲写像を別イベントで記録する必要があります。");
    True(
        traceSource.Contains("catch", StringComparison.Ordinal) &&
        traceSource.Contains(
            "Volatile.Write(ref _disabled, 1);",
            StringComparison.Ordinal) &&
        traceSource.Contains(
            "File.Move(temporaryPath, TraceFilePath, overwrite: true);",
            StringComparison.Ordinal),
        "トレース失敗を描画へ伝播せず、終了時に原子的に保存する必要があります。");

    var updateStart = compactEffectSource.IndexOf(
        "protectedoverridevoidUpdateConstants()",
        StringComparison.Ordinal);
    var uploadIndex = compactEffectSource.IndexOf(
        "drawInformation?.SetPixelShaderConstantBuffer(_gpuConstantBuffer);",
        updateStart,
        StringComparison.Ordinal);
    var constantsTraceIndex = compactEffectSource.IndexOf(
        "RenderCallbackTrace.RecordConstants(",
        updateStart,
        StringComparison.Ordinal);
    True(
        updateStart >= 0 && uploadIndex > updateStart &&
        constantsTraceIndex > uploadIndex,
        "定数送信に成功した後で描画時定数を記録する必要があります。");

    var mapInputStart = compactEffectSource.IndexOf(
        "publicoverridevoidMapInputRectsToOutputRect(",
        StringComparison.Ordinal);
    var outputAssignmentIndex = compactEffectSource.IndexOf(
        "outputRect=inputRects.Length>0?inputRects[0]:default;",
        mapInputStart,
        StringComparison.Ordinal);
    var mapInputTraceIndex = compactEffectSource.IndexOf(
        "RenderCallbackTrace.RecordMapInputRectsToOutputRect(",
        mapInputStart,
        StringComparison.Ordinal);
    True(
        mapInputStart >= 0 && outputAssignmentIndex > mapInputStart &&
        mapInputTraceIndex > outputAssignmentIndex,
        "出力範囲を算出した後でMapInput結果を記録する必要があります。");

    var mapOutputStart = compactEffectSource.IndexOf(
        "publicoverridevoidMapOutputRectToInputRects(",
        StringComparison.Ordinal);
    var maskAssignmentIndex = compactEffectSource.IndexOf(
        "inputRects[2]=newRawRect(0,0,WipeMaskRenderer.MaskSize,WipeMaskRenderer.MaskSize);",
        mapOutputStart,
        StringComparison.Ordinal);
    var mapOutputTraceIndex = compactEffectSource.IndexOf(
        "RenderCallbackTrace.RecordMapOutputRectToInputRects(",
        mapOutputStart,
        StringComparison.Ordinal);
    True(
        mapOutputStart >= 0 && maskAssignmentIndex > mapOutputStart &&
        mapOutputTraceIndex > maskAssignmentIndex,
        "全入力範囲を割り当てた後でMapOutput結果を記録する必要があります。");
    True(
        compactEffectSource.Contains(
            "#ifDEBUGUploadConstantsWithoutRenderCallbackTrace();#elseUpdateConstants();#endif",
            StringComparison.Ordinal),
        "プロパティ設定時の定数送信を描画コールバックとして記録してはいけません。");
    True(
        compactEffectSource.Contains(
            "privatevoidUploadConstantsWithoutRenderCallbackTrace(){_gpuConstantBuffer.Core=_constantBuffer;drawInformation?.SetPixelShaderConstantBuffer(_gpuConstantBuffer);}",
            StringComparison.Ordinal),
        "Debugのプロパティ設定時も従来と同じ定数送信を行う必要があります。");
    True(
        compactProcessorSource.Contains(
            "PreviewUpdateTrace.Flush();RenderCallbackTrace.Flush();#endif",
            StringComparison.Ordinal),
        "Processor解放時に両方の診断ログを確定する必要があります。");
}

static void VerifyScenePositionNormalization()
{
    var inputOrigin = new Vector2(-320, 180);
    var inputSize = new Vector2(1920, 1080);

    Vector2Equal(
        Vector2.Zero,
        NormalizeScenePosition(inputOrigin, inputOrigin, inputSize),
        "入力矩形左上は正規化座標0である必要があります。");
    Vector2Equal(
        new Vector2(0.5f, 0.5f),
        NormalizeScenePosition(
            inputOrigin + inputSize * 0.5f,
            inputOrigin,
            inputSize),
        "入力矩形中央はタイルに依存せず0.5である必要があります。");
    Vector2Equal(
        Vector2.One,
        NormalizeScenePosition(inputOrigin + inputSize, inputOrigin, inputSize),
        "入力矩形右下は正規化座標1である必要があります。");
}

static void VerifyFogAmountZeroPassThrough()
{
    var original = new Vector4(0.08f, 0.16f, 0.24f, 0.4f);
    var fogged = new Vector4(0.35f, 0.35f, 0.35f, 0.4f);
    var actual = CompositeReference(original, fogged, 0.8f, 0, 0.25f);

    VectorEqual(original, actual, "Fog Amount 0では元のRGBAを変更してはいけません。");
}

static void VerifyFogSettingsVisibility()
{
    foreach (var mode in new[] { GlassWipeEditingMode.Simple, GlassWipeEditingMode.Detailed })
    foreach (var path in Enum.GetValues<WipePathInputMode>())
    {
        var candidate = new GlassWipeVideoEffect { EditingMode = mode, PathInputMode = path };
        SetFogAmountValues(candidate, 25);
        True(candidate.AreFogNoiseSettingsVisible,
            "曇りノイズは設定方法や軌跡の種類にかかわらず正の曇り量で表示する必要があります。");
        SetFogAmountValues(candidate, 0);
        True(!candidate.AreFogNoiseSettingsVisible,
            "曇りノイズは固定0の曇り量で非表示にする必要があります。");
    }

    var effect = new GlassWipeVideoEffect
    {
        EditingMode = GlassWipeEditingMode.Detailed,
        PathInputMode = WipePathInputMode.LegacyAnimation,
    };

    SetFogAmountValues(effect, 0, 0);
    effect.FogAmount.AnimationType = YukkuriMovieMaker.Commons.AnimationType.直線移動;
    True(!effect.AreFogSettingsVisible && !effect.AreFogNoiseSettingsVisible,
        "有効な曇り量がすべて0なら関連設定を非表示にする必要があります。");

    SetFogAmountValues(effect, 25);
    True(effect.AreFogSettingsVisible && effect.AreFogNoiseSettingsVisible,
        "固定の正の曇り量では関連設定を表示する必要があります。");

#pragma warning disable CS0618
    effect.FogAmount.AnimationType = default;
    effect.FogAmount.From = 0;
    effect.FogAmount.To = 100;
#pragma warning restore CS0618
    True(!effect.AreFogSettingsVisible && !effect.AreFogNoiseSettingsVisible,
        "Animationなしで未使用の終了値だけが正でも関連設定を表示してはいけません。");

    effect.FogAmount.AnimationType = YukkuriMovieMaker.Commons.AnimationType.直線移動;
#pragma warning disable CS0618
    effect.FogAmount.From = 0;
    effect.FogAmount.To = 100;
#pragma warning restore CS0618
    True(effect.AreFogSettingsVisible && effect.AreFogNoiseSettingsVisible,
        "0から100へ変化する曇り量では関連設定を表示する必要があります。");

    SetFogAmountValues(effect, double.NaN);
    True(!effect.AreFogSettingsVisible && !effect.AreFogNoiseSettingsVisible,
        "非有限の曇り量は0として関連設定を非表示にする必要があります。");
    SetFogAmountValues(effect, -1);
    True(!effect.AreFogSettingsVisible && !effect.AreFogNoiseSettingsVisible,
        "負の曇り量は0として関連設定を非表示にする必要があります。");

    var notifiedProperties = new HashSet<string?>();
    effect.PropertyChanged += (_, eventArgs) => notifiedProperties.Add(eventArgs.PropertyName);
    SetFogAmountValues(effect, 0);
    True(
        notifiedProperties.Contains(nameof(GlassWipeVideoEffect.AreFogSettingsVisible)) &&
        notifiedProperties.Contains(nameof(GlassWipeVideoEffect.AreFogNoiseSettingsVisible)),
        "曇り量のAnimation変更時に両方の表示条件を通知する必要があります。");

    notifiedProperties.Clear();
    effect.FogAmount.ActiveValues.Single().Value = 10;
    True(
        notifiedProperties.Contains(nameof(GlassWipeVideoEffect.AreFogSettingsVisible)) &&
        notifiedProperties.Contains(nameof(GlassWipeVideoEffect.AreFogNoiseSettingsVisible)),
        "曇り量のAnimationValue変更時に両方の表示条件を通知する必要があります。");

    effect.OnDeserialized();
    notifiedProperties.Clear();
    effect.FogAmount.ActiveValues.Single().Value = 20;
    True(
        notifiedProperties.Contains(nameof(GlassWipeVideoEffect.AreFogSettingsVisible)) &&
        notifiedProperties.Contains(nameof(GlassWipeVideoEffect.AreFogNoiseSettingsVisible)),
        "復元完了後のAnimationValue変更でも両方の表示条件を通知する必要があります。");

    var effectSource = File.ReadAllText(
        Path.Combine(
            FindRepositoryRoot(),
            "src",
            "YMM4GlassWipe",
            "GlassWipeVideoEffect.cs"));
    foreach (var propertyName in new[] { "Blur", "FogTint", "TintMix" })
    {
        True(
            HasVisibilityCondition(effectSource, propertyName, "AreFogSettingsVisible"),
            $"{propertyName}は曇り量の表示条件へ結び付く必要があります。");
    }
    True(
        HasVisibilityCondition(effectSource, "FogNoise", "AreFogNoiseSettingsVisible"),
        "FogNoiseは曇り量の表示条件へ結び付く必要があります。");
}

static void SetFogAmountValues(GlassWipeVideoEffect effect, params double[] values)
{
    if (values.Length is < 1 or > 2)
    {
        throw new ArgumentException("曇り量は開始値と終了値の最大2件を指定してください。", nameof(values));
    }

    effect.FogAmount.AnimationType = default;
#pragma warning disable CS0618
    effect.FogAmount.From = values[0];
    effect.FogAmount.To = values[^1];
#pragma warning restore CS0618
}

static void VerifyAlphaPreservation()
{
    var original = new Vector4(0.05f, 0.1f, 0.2f, 0.25f);
    var fogged = new Vector4(0.4f, 0.3f, 0.2f, 0.9f);

    foreach (var fogAmount in new[] { 0f, 0.5f, 1f })
    {
        var actual = CompositeReference(original, fogged, 1, fogAmount, 0);
        NearlyEqual(
            original.W,
            actual.W,
            $"Fog Amount {fogAmount}でAlphaが変化しました。");
    }
}

static void VerifyRegionParameterClamp()
{
    NearlyEqual(0, GlassWipeParameterSanitizer.ToUnit(-1), "負の割合は0へClampします。");
    NearlyEqual(0.5f, GlassWipeParameterSanitizer.ToUnit(50), "50%は0.5へ変換します。");
    NearlyEqual(1, GlassWipeParameterSanitizer.ToUnit(101), "100%超は1へClampします。");
    NearlyEqual(0, GlassWipeParameterSanitizer.ToUnit(double.NaN), "NaNは0へ補正します。");
    NearlyEqual(-1, GlassWipeParameterSanitizer.ToQuadUnit(-150), "Quad座標は-100%へClampします。");
    NearlyEqual(-0.25f, GlassWipeParameterSanitizer.ToQuadUnit(-25), "Quadの負座標を維持します。");
    NearlyEqual(1.5f, GlassWipeParameterSanitizer.ToQuadUnit(150), "Quadの100%超座標を維持します。");
    NearlyEqual(2, GlassWipeParameterSanitizer.ToQuadUnit(250), "Quad座標は200%へClampします。");
    NearlyEqual(
        0,
        GlassWipeParameterSanitizer.Clamp(-1, 0, 1000),
        "負のRegion Featherは0へClampします。");
    NearlyEqual(
        1000,
        GlassWipeParameterSanitizer.Clamp(1001, 0, 1000),
        "Region Featherの上限は1000pxです。");
}

static void VerifyOutsideDropletSettingsAndUi()
{
    var effectSource = File.ReadAllText(
        Path.Combine(
            FindRepositoryRoot(),
            "src",
            "YMM4GlassWipe",
            "GlassWipeVideoEffect.cs"));
    OutsideDropletUiVerification.VerifyValuesAndNotifications();
    True(
        effectSource.Contains(
            "GroupName = \"水滴\", Name = \"外側の水滴量\"",
            StringComparison.Ordinal) &&
        effectSource.Contains("拭き取りでは水滴が残り", StringComparison.Ordinal),
        "外側水滴量のツールチップへ拭けない注意を表示する必要があります。");

    foreach (var propertyName in new[]
             {
                 "OutsideDropletSize",
                 "OutsideDropletStrength",
                 "OutsideDropletSeed",
                 "OutsideDropletDeformWithSurface",
                 "OutsideDropletFallEnabled",
             })
    {
        True(
            HasVisibilityCondition(
                effectSource,
                propertyName,
                propertyName == "OutsideDropletDeformWithSurface"
                    ? "IsOutsideDropletDeformWithSurfaceVisible"
                    : "AreOutsideDropletSettingsVisible"),
            $"{propertyName}は水滴量の表示条件へ結び付く必要があります。");
    }

    foreach (var propertyName in new[]
             {
                 "OutsideDropletFallingRatio",
                 "OutsideDropletFallSpeed",
                 "OutsideDropletTrailLength",
             })
    {
        True(
            HasVisibilityCondition(
                effectSource,
                propertyName,
                propertyName is "OutsideDropletFallingRatio" or "OutsideDropletFallFrequency"
                    ? "AreOutsideDropletLegacyFallSettingsVisible"
                    : "AreOutsideDropletFallSettingsVisible"),
            $"{propertyName}はモード別の落下表示条件へ結び付く必要があります。");
    }

    NearlyEqual(0, OutsideDropletSettings.SanitizeAmount(-1), "水滴量下限", 0.001f);
    NearlyEqual(1, OutsideDropletSettings.SanitizeAmount(101), "水滴量上限", 0.001f);
    NearlyEqual(0, OutsideDropletSettings.SanitizeAmount(double.NaN), "水滴量NaN既定値", 0.001f);
    NearlyEqual(0.25f, OutsideDropletSettings.SanitizeSizeScale(1), "水滴サイズ下限", 0.001f);
    NearlyEqual(4, OutsideDropletSettings.SanitizeSizeScale(500), "水滴サイズ上限", 0.001f);
    NearlyEqual(1, OutsideDropletSettings.SanitizeSizeScale(double.NaN), "水滴サイズNaN既定値", 0.001f);
    NearlyEqual(0.5f, OutsideDropletSettings.SanitizeStrength(double.NaN), "水滴濃さNaN既定値", 0.001f);
    NearlyEqual(1, OutsideDropletSettings.SanitizeSeed(double.NaN), "水滴Seed NaN既定値", 0.001f);
    NearlyEqual(0, OutsideDropletSettings.SanitizeSeed(-1), "水滴Seed下限", 0.001f);
    NearlyEqual(9999, OutsideDropletSettings.SanitizeSeed(10000), "水滴Seed上限", 0.001f);
    NearlyEqual(2, OutsideDropletSettings.SanitizeSeed(1.6), "水滴Seed整数化", 0.001f);
    Equal(
        33.3,
        OutsideDropletSettings.SanitizeAmountPercent(33.3),
        "保存用の水滴量はdouble精度を維持する必要があります。");
    NearlyEqual(0, OutsideDropletSettings.SanitizeFallingRatio(-1), "落下割合下限", 0.001f);
    NearlyEqual(1, OutsideDropletSettings.SanitizeFallingRatio(101), "落下割合上限", 0.001f);
    NearlyEqual(0.3f, OutsideDropletSettings.SanitizeFallingRatio(double.NaN), "落下割合NaN既定値", 0.001f);
    NearlyEqual(0.25f, OutsideDropletSettings.SanitizeFallSpeedScale(-1), "落下速度下限", 0.001f);
    NearlyEqual(4, OutsideDropletSettings.SanitizeFallSpeedScale(500), "落下速度上限", 0.001f);
    NearlyEqual(1, OutsideDropletSettings.SanitizeFallSpeedScale(double.NaN), "落下速度NaN既定値", 0.001f);
    NearlyEqual(0, OutsideDropletSettings.SanitizeTrailLength(-1), "水筋長さ下限", 0.001f);
    NearlyEqual(1, OutsideDropletSettings.SanitizeTrailLength(101), "水筋長さ上限", 0.001f);
    NearlyEqual(0.35f, OutsideDropletSettings.SanitizeTrailLength(double.NaN), "水筋長さNaN既定値", 0.001f);
    True(
        effectSource.Contains("同じ設定と時刻", StringComparison.Ordinal) &&
        effectSource.Contains("画面下方向への落下", StringComparison.Ordinal) &&
        effectSource.Contains("長い水筋ほど描画負荷", StringComparison.Ordinal),
        "落下、変形、負荷の注意をツールチップへ表示する必要があります。");

    Equal(30d, OutsideDropletSettings.DefaultFallingRatioPercent, "落下割合の既定値");
    Equal(100d, OutsideDropletSettings.DefaultFallSpeedPercent, "落下速度の既定値");
    Equal(35d, OutsideDropletSettings.DefaultTrailLengthPercent, "水筋長さの既定値");
}

static void VerifyOutsideDropletPresetScope()
{
    var current = DetailedWipePresetFactory.Create(DetailedWipeBuiltInPreset.Heart);
    current.OutsideDropletAmount = 15;
    current.OutsideDropletSize = 125;
    current.OutsideDropletStrength = 35;
    current.OutsideDropletSeed = 9;
    current.OutsideDropletDeformWithSurface = false;
    current.OutsideDropletFallEnabled = true;
    current.OutsideDropletFallingRatio = 25;
    current.OutsideDropletFallSpeed = 75;
    current.OutsideDropletTrailLength = 20;
    var preset = DetailedWipePresetFactory.Create(DetailedWipeBuiltInPreset.Smiley);
    preset.OutsideDropletAmount = 80;
    preset.OutsideDropletSize = 200;
    preset.OutsideDropletStrength = 90;
    preset.OutsideDropletSeed = 42;
    preset.OutsideDropletDeformWithSurface = true;
    preset.OutsideDropletFallEnabled = false;
    preset.OutsideDropletFallingRatio = 70;
    preset.OutsideDropletFallSpeed = 250;
    preset.OutsideDropletTrailLength = 80;

    foreach (var scope in new[] { DetailedWipePresetScope.Path, DetailedWipePresetScope.Brush })
    {
        True(
            DetailedWipePresetStateMerger.TryMerge(current, preset, scope, out var partial),
            $"{scope}プリセットを統合できる必要があります。");
        NearlyEqual(15, (float)(partial.OutsideDropletAmount ?? -1), $"{scope}で水滴量を保持", 0.001f);
        NearlyEqual(125, (float)(partial.OutsideDropletSize ?? -1), $"{scope}で水滴サイズを保持", 0.001f);
        NearlyEqual(35, (float)(partial.OutsideDropletStrength ?? -1), $"{scope}で水滴濃さを保持", 0.001f);
        NearlyEqual(9, (float)(partial.OutsideDropletSeed ?? -1), $"{scope}で水滴Seedを保持", 0.001f);
        Equal(false, partial.OutsideDropletDeformWithSurface, $"{scope}で面変形を保持");
        Equal(true, partial.OutsideDropletFallEnabled, $"{scope}で落下ONを保持");
        NearlyEqual(25, (float)(partial.OutsideDropletFallingRatio ?? -1), $"{scope}で落下割合を保持", 0.001f);
        NearlyEqual(75, (float)(partial.OutsideDropletFallSpeed ?? -1), $"{scope}で落下速度を保持", 0.001f);
        NearlyEqual(20, (float)(partial.OutsideDropletTrailLength ?? -1), $"{scope}で水筋長さを保持", 0.001f);
        True(preset.TrySanitize(scope, out var scoped), $"{scope}を正規化できる必要があります。");
        True(
            scoped.OutsideDropletAmount is null &&
            scoped.OutsideDropletSize is null &&
            scoped.OutsideDropletStrength is null &&
            scoped.OutsideDropletSeed is null &&
            scoped.OutsideDropletDeformWithSurface is null &&
            scoped.OutsideDropletFallEnabled is null &&
            scoped.OutsideDropletFallingRatio is null &&
            scoped.OutsideDropletFallSpeed is null &&
            scoped.OutsideDropletTrailLength is null,
            $"{scope}プリセットへ水滴設定を含めてはいけません。");
    }

    var brokenCurrent = DetailedWipePresetFactory.Create(DetailedWipeBuiltInPreset.Heart);
    brokenCurrent.CustomPathData = "{broken";
    brokenCurrent.OutsideDropletAmount = 15;
    True(
        DetailedWipePresetStateMerger.TryMerge(
            brokenCurrent,
            preset,
            DetailedWipePresetScope.Brush,
            out var brushAppliedToBrokenPath),
        "現在の軌跡データが破損していてもブラシプリセットを適用できる必要があります。");
    NearlyEqual(
        15,
        (float)(brushAppliedToBrokenPath.OutsideDropletAmount ?? -1),
        "破損軌跡へのブラシ適用でも現在の水滴量を保持",
        0.001f);
    Equal(
        preset.BrushShape,
        brushAppliedToBrokenPath.BrushShape,
        "破損軌跡へのブラシ適用でもブラシ形状を適用する必要があります。");
    True(
        WipeStrokeDocumentCodec.TryDecode(brushAppliedToBrokenPath.CustomPathData, out _),
        "破損した現在軌跡は安全な軌跡へ補正する必要があります。");

    True(
        DetailedWipePresetStateMerger.TryMerge(
            current,
            preset,
            DetailedWipePresetScope.All,
            out var all),
        "全設定プリセットを統合できる必要があります。");
    NearlyEqual(80, (float)(all.OutsideDropletAmount ?? -1), "全設定の水滴量", 0.001f);
    NearlyEqual(200, (float)(all.OutsideDropletSize ?? -1), "全設定の水滴サイズ", 0.001f);
    NearlyEqual(90, (float)(all.OutsideDropletStrength ?? -1), "全設定の水滴濃さ", 0.001f);
    NearlyEqual(42, (float)(all.OutsideDropletSeed ?? -1), "全設定の水滴Seed", 0.001f);
    Equal(true, all.OutsideDropletDeformWithSurface, "全設定の面変形");
    Equal(false, all.OutsideDropletFallEnabled, "全設定の落下ON/OFF");
    NearlyEqual(70, (float)(all.OutsideDropletFallingRatio ?? -1), "全設定の落下割合", 0.001f);
    NearlyEqual(250, (float)(all.OutsideDropletFallSpeed ?? -1), "全設定の落下速度", 0.001f);
    NearlyEqual(80, (float)(all.OutsideDropletTrailLength ?? -1), "全設定の水筋長さ", 0.001f);

    var encoded = DetailedWipePresetCodec.Encode(preset);
    True(
        encoded.Contains("\"outsideDropletAmount\":80", StringComparison.Ordinal) &&
        encoded.Contains("\"outsideDropletDeformWithSurface\":true", StringComparison.Ordinal) &&
        encoded.Contains("\"outsideDropletFallEnabled\":false", StringComparison.Ordinal) &&
        encoded.Contains("\"outsideDropletFallingRatio\":70", StringComparison.Ordinal) &&
        encoded.Contains("\"outsideDropletFallSpeed\":250", StringComparison.Ordinal) &&
        encoded.Contains("\"outsideDropletTrailLength\":80", StringComparison.Ordinal),
        "全設定プリセットへ外側水滴9設定を保存する必要があります。");
    var legacyWithoutMotion = encoded
        .Replace(",\"outsideDropletDeformWithSurface\":true", string.Empty, StringComparison.Ordinal)
        .Replace(",\"outsideDropletFallEnabled\":false", string.Empty, StringComparison.Ordinal)
        .Replace(",\"outsideDropletFallingRatio\":70", string.Empty, StringComparison.Ordinal)
        .Replace(",\"outsideDropletFallSpeed\":250", string.Empty, StringComparison.Ordinal)
        .Replace(",\"outsideDropletTrailLength\":80", string.Empty, StringComparison.Ordinal);
    var legacyV7 = legacyWithoutMotion.Replace(
        $"\"dataSchemaVersion\":{DetailedWipePresetState.CurrentVersion}",
        "\"dataSchemaVersion\":7",
        StringComparison.Ordinal);
    True(
        DetailedWipePresetCodec.TryDecode(legacyV7, out var migratedV7),
        "v7プリセットを現行Stateへ移行できる必要があります。");
    NearlyEqual(80, (float)(migratedV7.OutsideDropletAmount ?? -1), "v7の水滴量を保持", 0.001f);
    NearlyEqual(42, (float)(migratedV7.OutsideDropletSeed ?? -1), "v7の水滴Seedを保持", 0.001f);
    Equal(true, migratedV7.OutsideDropletDeformWithSurface, "v7の面変形既定値");
    Equal(false, migratedV7.OutsideDropletFallEnabled, "v7の落下既定値");
    NearlyEqual(30, (float)(migratedV7.OutsideDropletFallingRatio ?? -1), "v7の落下割合既定値", 0.001f);
    NearlyEqual(100, (float)(migratedV7.OutsideDropletFallSpeed ?? -1), "v7の落下速度既定値", 0.001f);
    NearlyEqual(35, (float)(migratedV7.OutsideDropletTrailLength ?? -1), "v7の水筋長さ既定値", 0.001f);

    var legacyWithoutDroplets = legacyWithoutMotion
        .Replace(",\"outsideDropletAmount\":80", string.Empty, StringComparison.Ordinal)
        .Replace(",\"outsideDropletSize\":200", string.Empty, StringComparison.Ordinal)
        .Replace(",\"outsideDropletStrength\":90", string.Empty, StringComparison.Ordinal)
        .Replace(",\"outsideDropletSeed\":42", string.Empty, StringComparison.Ordinal);
    for (var legacyVersion = 1; legacyVersion <= 6; legacyVersion++)
    {
        var legacy = legacyWithoutDroplets.Replace(
            $"\"dataSchemaVersion\":{DetailedWipePresetState.CurrentVersion}",
            $"\"dataSchemaVersion\":{legacyVersion}",
            StringComparison.Ordinal);
        True(
            DetailedWipePresetCodec.TryDecode(legacy, out var migrated),
            $"水滴設定がないv{legacyVersion}プリセットを読み込める必要があります。");
        NearlyEqual(0, (float)(migrated.OutsideDropletAmount ?? -1), $"v{legacyVersion}の水滴量既定値", 0.001f);
        NearlyEqual(100, (float)(migrated.OutsideDropletSize ?? -1), $"v{legacyVersion}の水滴サイズ既定値", 0.001f);
        NearlyEqual(50, (float)(migrated.OutsideDropletStrength ?? -1), $"v{legacyVersion}の水滴濃さ既定値", 0.001f);
        NearlyEqual(1, (float)(migrated.OutsideDropletSeed ?? -1), $"v{legacyVersion}の水滴Seed既定値", 0.001f);
        Equal(true, migrated.OutsideDropletDeformWithSurface, $"v{legacyVersion}の面変形既定値");
        Equal(false, migrated.OutsideDropletFallEnabled, $"v{legacyVersion}の落下既定値");
        NearlyEqual(30, (float)(migrated.OutsideDropletFallingRatio ?? -1), $"v{legacyVersion}の落下割合既定値", 0.001f);
        NearlyEqual(100, (float)(migrated.OutsideDropletFallSpeed ?? -1), $"v{legacyVersion}の落下速度既定値", 0.001f);
        NearlyEqual(35, (float)(migrated.OutsideDropletTrailLength ?? -1), $"v{legacyVersion}の水筋長さ既定値", 0.001f);
    }

    var future = encoded.Replace(
        $"\"dataSchemaVersion\":{DetailedWipePresetState.CurrentVersion}",
        $"\"dataSchemaVersion\":{DetailedWipePresetState.CurrentVersion + 1}",
        StringComparison.Ordinal);
    True(!DetailedWipePresetCodec.TryDecode(future, out _), "未知の将来版プリセットを拒否する必要があります。");
}

static void VerifyOutsideDropletAllPresetEdgeValueRoundTrip()
{
    var temporaryDirectory = Path.Combine(
        Path.GetTempPath(),
        $"YMM4GlassWipe.OutsideDropletPreset.{Guid.NewGuid():N}");
    Directory.CreateDirectory(temporaryDirectory);
    try
    {
        var lower = CreateOutsideDropletPresetState(
            amount: 0,
            size: 25,
            strength: 0,
            seed: 0,
            deformWithSurface: false,
            fallEnabled: true,
            fallingRatio: 0,
            fallSpeed: 25,
            trailLength: 0);
        var upper = CreateOutsideDropletPresetState(
            amount: 100,
            size: 400,
            strength: 100,
            seed: 9999,
            deformWithSurface: true,
            fallEnabled: false,
            fallingRatio: 100,
            fallSpeed: 400,
            trailLength: 100);
        upper.OutsideDropletAppearance = OutsideDropletAppearance.Transparent;
        var edgeCases = new[] { lower, upper };
        var allFilePath = Path.Combine(temporaryDirectory, "all", "presets.json");
        var allStore = new DetailedWipeUserPresetStore(allFilePath);
        for (var index = 0; index < edgeCases.Length; index++)
        {
            True(
                allStore.Upsert(
                    $"境界{index}",
                    DetailedWipePresetScope.All,
                    edgeCases[index],
                    out var replaced,
                    out var error),
                error ?? $"全設定境界{index}の保存に失敗しました。");
            True(!replaced, $"全設定境界{index}を初回置換扱いにしてはいけません。");
        }

        var allPresets = allStore.Load(out var loadError);
        True(loadError is null, loadError ?? "全設定境界値の再読込に失敗しました。");
        Equal(2, allPresets.Count, "全設定境界値の保存件数");
        for (var index = 0; index < edgeCases.Length; index++)
        {
            var loaded = allPresets.Single(preset => preset.Name == $"境界{index}");
            Equal(DetailedWipePresetScope.All, loaded.Scope, $"境界{index}の保存種類");
            OutsideDropletPresetStateEqual(
                edgeCases[index],
                loaded.State,
                $"全設定境界{index}のファイル往復");
            True(
                DetailedWipePresetStateMerger.TryMerge(
                    edgeCases[1 - index],
                    loaded.State,
                    DetailedWipePresetScope.All,
                    out var merged),
                $"全設定境界{index}を統合できる必要があります。");
            OutsideDropletPresetStateEqual(
                edgeCases[index],
                merged,
                $"全設定境界{index}の適用結果");
        }

        using (var allJson = JsonDocument.Parse(File.ReadAllText(allFilePath)))
        {
            Equal(
                DetailedWipeUserPresetFile.CurrentVersion,
                allJson.RootElement.GetProperty("dataSchemaVersion").GetInt32(),
                "ユーザープリセットファイルのschema version");
            foreach (var preset in allJson.RootElement.GetProperty("presets").EnumerateArray())
            {
                var state = preset.GetProperty("state");
                Equal(DetailedWipePresetState.CurrentVersion, state.GetProperty("dataSchemaVersion").GetInt32(), "全設定Stateの現行版");
                foreach (var propertyName in OutsideDropletPresetPropertyNames())
                {
                    True(
                        state.TryGetProperty(propertyName, out _),
                        $"全設定ファイルへ{propertyName}を保存する必要があります。");
                }
            }
        }

        var scopedFilePath = Path.Combine(temporaryDirectory, "scoped", "presets.json");
        var scopedStore = new DetailedWipeUserPresetStore(scopedFilePath);
        foreach (var scope in new[] { DetailedWipePresetScope.Path, DetailedWipePresetScope.Brush })
        {
            True(
                scopedStore.Upsert(
                    $"種類{scope}",
                    scope,
                    upper,
                    out _,
                    out var error),
                error ?? $"{scope}プリセットの保存に失敗しました。");
        }

        using (var scopedJson = JsonDocument.Parse(File.ReadAllText(scopedFilePath)))
        {
            foreach (var preset in scopedJson.RootElement.GetProperty("presets").EnumerateArray())
            {
                var state = preset.GetProperty("state");
                foreach (var propertyName in OutsideDropletPresetPropertyNames())
                {
                    True(
                        !state.TryGetProperty(propertyName, out _),
                        $"部分プリセットへ{propertyName}を保存してはいけません。");
                }
            }
        }

        var scopedPresets = scopedStore.Load(out loadError);
        True(loadError is null, loadError ?? "部分プリセットの再読込に失敗しました。");
        foreach (var scopedPreset in scopedPresets)
        {
            True(
                DetailedWipePresetStateMerger.TryMerge(
                    lower,
                    scopedPreset.State,
                    scopedPreset.Scope,
                    out var merged),
                $"{scopedPreset.Scope}プリセットを統合できる必要があります。");
            OutsideDropletPresetStateEqual(
                lower,
                merged,
                $"{scopedPreset.Scope}適用時の水滴設定保持");
        }
    }
    finally
    {
        Directory.Delete(temporaryDirectory, recursive: true);
    }
}

static void VerifyOutsideDropletShaderContract()
{
    var shaderSource = ShaderVerificationSource.ReadAllText(
        Path.Combine(
            FindRepositoryRoot(),
            "src",
            "YMM4GlassWipe",
            "Shaders",
            "GlassComposite.hlsl")).Replace("\r\n", "\n", StringComparison.Ordinal);
    True(
        shaderSource.Contains("float outsideDropletAmount : packoffset(c9.x);", StringComparison.Ordinal) &&
        shaderSource.Contains("float outsideDropletSizeScale : packoffset(c9.y);", StringComparison.Ordinal) &&
        shaderSource.Contains("float outsideDropletStrength : packoffset(c9.z);", StringComparison.Ordinal) &&
        shaderSource.Contains("float outsideDropletSeed : packoffset(c9.w);", StringComparison.Ordinal) &&
        shaderSource.Contains("float outsideDropletLocalTimeSeconds : packoffset(c10.x);", StringComparison.Ordinal) &&
        shaderSource.Contains("float outsideDropletFallEnabled : packoffset(c10.y);", StringComparison.Ordinal) &&
        shaderSource.Contains("float outsideDropletFallingRatio : packoffset(c10.z);", StringComparison.Ordinal) &&
        shaderSource.Contains("float outsideDropletFallSpeedScale : packoffset(c10.w);", StringComparison.Ordinal) &&
        shaderSource.Contains("float outsideDropletTrailLength : packoffset(c11.x);", StringComparison.Ordinal) &&
        shaderSource.Contains("float outsideDropletDeformWithSurface : packoffset(c11.y);", StringComparison.Ordinal) &&
        shaderSource.Contains("float3 quadForwardRow0 : packoffset(c12.x);", StringComparison.Ordinal) &&
        shaderSource.Contains("float3 quadForwardRow1 : packoffset(c13.x);", StringComparison.Ordinal) &&
        shaderSource.Contains("float3 quadForwardRow2 : packoffset(c14.x);", StringComparison.Ordinal),
        "HLSLのc9-c14とC#定数バッファを同期する必要があります。");
    True(
        !shaderSource.Contains("if (fogAmount <= 0.0f)", StringComparison.Ordinal),
        "曇り量0でも外側水滴を表示できる必要があります。");
    var prepassStart = shaderSource.IndexOf(
        "if (outsideDropletRenderPass >= 0.5f)", StringComparison.Ordinal);
    var clearDropletStart = shaderSource.IndexOf(
        "float4 clearWithDroplets = original;", prepassStart, StringComparison.Ordinal);
    var prepassReturn = shaderSource.IndexOf(
        "return clearWithDroplets;", clearDropletStart, StringComparison.Ordinal);
    var blurSample = shaderSource.IndexOf(
        "float4 blurred = BlurredTexture.Sample", prepassReturn, StringComparison.Ordinal);
    var resultStart = shaderSource.IndexOf(
        "float4 result = original;", blurSample, StringComparison.Ordinal);
    var alphaRestore = shaderSource.IndexOf(
        "result.a = original.a;", resultStart, StringComparison.Ordinal);
    True(
        prepassStart >= 0 && clearDropletStart > prepassStart &&
        prepassReturn > clearDropletStart && blurSample > prepassReturn &&
        resultStart > blurSample && alphaRestore > resultStart,
        "水滴を前処理で合成し、後段でぼかし入力と曇りを合成する必要があります。");
    var dropletBlock = shaderSource[clearDropletStart..prepassReturn];
    True(
        dropletBlock.Contains("regionMask", StringComparison.Ordinal),
        "外側水滴を領域と境界ぼかしへ追従させる必要があります。");
    True(
        !dropletBlock.Contains("effectiveWipeMask", StringComparison.Ordinal) &&
        !dropletBlock.Contains("sampledWipeMask", StringComparison.Ordinal) &&
        !dropletBlock.Contains("MaskTexture", StringComparison.Ordinal),
        "外側水滴へ拭き取りマスクを適用してはいけません。");
    True(
        shaderSource.Contains("clearWithDroplets.rgb = clearStraight * original.a;", StringComparison.Ordinal) &&
        shaderSource.Contains("result = lerp(original, fogged, fogMask);", StringComparison.Ordinal) &&
        shaderSource.Contains("result.a = original.a;", StringComparison.Ordinal) &&
        !shaderSource.Contains("foggedWithDroplets", StringComparison.Ordinal),
        "水滴を含む映像へくもりを重ね、鮮明な滴を重ね直さず入力Alphaを維持する必要があります。");
    True(
        shaderSource.Contains(
            "headInputUv = spawnInputUv +\n        float2(0.0f, fallDistance / safeInputSize.y)",
            StringComparison.Ordinal) &&
        shaderSource.Contains("MapRegionUvToInputUv", StringComparison.Ordinal) &&
        shaderSource.Contains("outsideDropletDeformWithSurface >= 0.5f", StringComparison.Ordinal),
        "落下は画面下方向とし、Quadの面変形OFFを実装する必要があります。");
    True(
        shaderSource.Contains("if (outsideDropletFallEnabled >= 0.5f", StringComparison.Ordinal) &&
        shaderSource.Contains("outsideDropletTrailLength", StringComparison.Ordinal) &&
        !shaderSource.Contains("Previous", StringComparison.OrdinalIgnoreCase),
        "落下OFFの静的分岐、水筋、前フレーム非依存を維持する必要があります。");
    True(
        shaderSource.Contains("static const int OutsideDropletBaseEmitterCount = 6;", StringComparison.Ordinal) &&
        shaderSource.Contains("uint emitterKey = GetOutsideDropletEmitterKey(", StringComparison.Ordinal) &&
        shaderSource.Contains("uint cycleKey = GetOutsideDropletCycleKey(", StringComparison.Ordinal) &&
        shaderSource.Contains("emitterIndex < emitterCount", StringComparison.Ordinal) &&
        shaderSource.Contains("emitterCount = OutsideDropletBaseEmitterCount * (int)ceil(clamp(outsideDropletFallFrequency, 1.0f, 4.0f))", StringComparison.Ordinal) &&
        shaderSource.Contains("return CombineOutsideDropletSamples(samples, sampleCount);", StringComparison.Ordinal),
        "静的格子を維持し、基準6個体×3層を頻度で増やし個別Seedで評価する必要があります。");
    True(
        shaderSource.Contains("float visibleWaitDuration", StringComparison.Ordinal) &&
        shaderSource.Contains("float hiddenWaitDuration", StringComparison.Ordinal) &&
        shaderSource.Contains("return float4(progress, visibility, cycleIndex, 1.0f);", StringComparison.Ordinal),
        "落下個体は開始位置の待機、落下、非表示待機を順に評価する必要があります。");
    True(
        shaderSource.Contains("EvaluateOutsideDropletCapsule", StringComparison.Ordinal) &&
        shaderSource.Contains("trailStartInputUv = lerp(", StringComparison.Ordinal) &&
        shaderSource.Contains("spawnInputUv,", StringComparison.Ordinal) &&
        shaderSource.Contains("trailLength);", StringComparison.Ordinal) &&
        !shaderSource.Contains("trailStretch", StringComparison.Ordinal),
        "水筋100%は固定長上限なしで開始位置までの線分を評価する必要があります。");
    Equal(
        3,
        shaderSource.Split("Texture2D ", StringSplitOptions.None).Length - 1,
        "水滴用の外部テクスチャ入力を追加してはいけません。");

}

static void VerifyOutsideDropletStaticStabilityReference()
{
    var shaderSource = ShaderVerificationSource.ReadAllText(
        Path.Combine(
            FindRepositoryRoot(),
            "src",
            "YMM4GlassWipe",
            "Shaders",
            "GlassComposite.hlsl"));
    var staticLayerSource = ExtractHlslFunction(
        shaderSource,
        "float3 EvaluateOutsideDropletLayer(");
    True(
        !staticLayerSource.Contains("outsideDropletLocalTimeSeconds", StringComparison.Ordinal) &&
        !staticLayerSource.Contains("outsideDropletFallEnabled", StringComparison.Ordinal) &&
        !staticLayerSource.Contains("outsideDropletFallingRatio", StringComparison.Ordinal) &&
        !staticLayerSource.Contains("outsideDropletFallSpeedScale", StringComparison.Ordinal) &&
        !staticLayerSource.Contains("outsideDropletTrailLength", StringComparison.Ordinal),
        "静止水滴の形状へ時刻または落下設定を混入させてはいけません。");
    True(
        staticLayerSource.Contains(
            "EvaluateOutsideDropletAppearance(",
            StringComparison.Ordinal),
        "静止水滴は落下変形0の形状を使用する必要があります。");
    var staticAppearanceSource = ExtractHlslFunction(
        shaderSource,
        "float3 EvaluateOutsideDropletAppearance(");
    True(
        !staticAppearanceSource.Contains("fwidth(dropletDistance)", StringComparison.Ordinal) &&
        staticLayerSource.Contains("ddx(patternPosition)", StringComparison.Ordinal) &&
        staticLayerSource.Contains("ddy(patternPosition)", StringComparison.Ordinal) &&
        staticLayerSource.Contains("sourcePixelStep / minimumRadius", StringComparison.Ordinal),
        "静止水滴はセル境界で不連続な距離ではなく、連続座標の画素幅で輪郭をぼかす必要があります。");

    var compositeSource = ExtractHlslFunction(
        shaderSource,
        "float3 CompositeOutsideDropletSurface(");
    True(
        compositeSource.Contains("stableSurfaceColor", StringComparison.Ordinal) &&
        compositeSource.Contains("dropletLighting.z", StringComparison.Ordinal) &&
        compositeSource.Contains("lerp(", StringComparison.Ordinal),
        "背景と独立した面被覆を経由して水滴を合成する必要があります。");
    var dropletMainStart = shaderSource.IndexOf(
        "float3 clearStraight =",
        StringComparison.Ordinal);
    var dropletMainEnd = shaderSource.IndexOf(
        "clearWithDroplets.rgb =",
        dropletMainStart,
        StringComparison.Ordinal);
    True(
        dropletMainStart >= 0 && dropletMainEnd > dropletMainStart,
        "水滴のclear側合成ブロックが見つかりません。");
    var clearComposition = shaderSource[dropletMainStart..dropletMainEnd];
    True(
        clearComposition.Contains("CompositeOutsideDropletSurface(", StringComparison.Ordinal) &&
        !clearComposition.Contains("dropletLighting.x *", StringComparison.Ordinal) &&
        !clearComposition.Contains("dropletLighting.y *", StringComparison.Ordinal),
        "水滴の明暗を背景へ直接加減算せず、安定合成関数を使用する必要があります。");

    var backgrounds = new[]
    {
        Vector3.Zero,
        new Vector3(0.5f, 0.5f, 0.5f),
        Vector3.One,
        new Vector3(0.1f, 0.7f, 0.3f),
    };
    var lighting = new Vector3(0.65f, 0.35f, 0.8f);
    float? expectedCoverage = null;
    foreach (var background in backgrounds)
    {
        var composed = ReferenceCompositeOutsideDropletSurface(
            background,
            lighting,
            0.75f,
            0.16f,
            0.28f,
            0.22f);
        True(
            float.IsFinite(composed.Color.X) &&
            float.IsFinite(composed.Color.Y) &&
            float.IsFinite(composed.Color.Z) &&
            composed.Color.X is >= 0 and <= 1 &&
            composed.Color.Y is >= 0 and <= 1 &&
            composed.Color.Z is >= 0 and <= 1,
            "安定合成のRGBは有限な0から1である必要があります。");
        if (expectedCoverage is null)
        {
            expectedCoverage = composed.Coverage;
        }
        else
        {
            NearlyEqual(
                expectedCoverage.Value,
                composed.Coverage,
                "面被覆は背景色へ依存してはいけません。",
                0.000001f);
        }

        var noDroplet = ReferenceCompositeOutsideDropletSurface(
            background,
            Vector3.Zero,
            1,
            0.16f,
            0.28f,
            0.22f);
        NearlyEqual(background.X, noDroplet.Color.X, "被覆0のR", 0.000001f);
        NearlyEqual(background.Y, noDroplet.Color.Y, "被覆0のG", 0.000001f);
        NearlyEqual(background.Z, noDroplet.Color.Z, "被覆0のB", 0.000001f);
    }
}

static void VerifyFallingDropletShapeReference()
{
    var shaderSource = ShaderVerificationSource.ReadAllText(
        Path.Combine(
            FindRepositoryRoot(),
            "src",
            "YMM4GlassWipe",
            "Shaders",
            "GlassComposite.hlsl"));
    var shapeSource = ExtractHlslFunction(
        shaderSource,
        "float2 ShapeFallingOutsideDroplet(");
    True(
        shapeSource.Contains("GetFallingOutsideDropletWidthProfile(", StringComparison.Ordinal) &&
        shapeSource.Contains("fallProgress", StringComparison.Ordinal),
        "落下頭部は進行度に応じた上細下丸の幅変形を使用する必要があります。");
    var fallingSource = ExtractHlslFunction(
        shaderSource,
        "float3 EvaluateFallingOutsideDropletEmitter(") + ExtractHlslFunction(
        shaderSource, "float3 RenderOutsideDropletHeadAndTrail(");
    True(
        fallingSource.Contains("upperAnchorPixels", StringComparison.Ordinal) &&
        fallingSource.Contains("trailEndInputUv", StringComparison.Ordinal) &&
        fallingSource.Contains("trailEndRegionUv", StringComparison.Ordinal) &&
        fallingSource.Contains("MergeOutsideDropletTrail(dropletLighting, trailLighting)", StringComparison.Ordinal),
        "水筋は水滴中心ではなく、変形後の上端アンカーへ接続する必要があります。");
    var fallingAppearanceSource = ExtractHlslFunction(
        shaderSource,
        "float3 EvaluateFallingOutsideDropletAppearance(");
    var capsuleSource = ExtractHlslFunction(
        shaderSource,
        "float3 EvaluateOutsideDropletCapsule(");
    var mappedTrailSource = ExtractHlslFunction(
        shaderSource,
        "float3 EvaluateMappedOutsideDropletTrailSegment(");
    var regionPixelStepSource = ExtractHlslFunction(
        shaderSource,
        "float GetOutsideDropletRegionPixelsPerInputPixel(");
    True(
        fallingSource.Contains(
            "EvaluateFallingOutsideDropletAppearance(",
            StringComparison.Ordinal) &&
        fallingSource.Contains(
            "GetOutsideDropletRegionPixelsPerInputPixel(",
            StringComparison.Ordinal) &&
        !fallingSource.Contains("fwidth(", StringComparison.Ordinal),
        "落下headは入力1px換算の専用appearanceを使用し、画面微分へ依存してはいけません。");
    True(
        fallingAppearanceSource.Contains("sourcePixelStep", StringComparison.Ordinal) &&
        fallingAppearanceSource.Contains("GetFallingOutsideDropletWidthProfile(", StringComparison.Ordinal) &&
        fallingAppearanceSource.Contains("normalizedGradientY", StringComparison.Ordinal) &&
        fallingAppearanceSource.Contains("axialCoverage", StringComparison.Ordinal) &&
        !fallingAppearanceSource.Contains("fwidth(", StringComparison.Ordinal),
        "落下headの輪郭幅は解析的に求める必要があります。");
    True(
        capsuleSource.Contains("edgeWidth = max(edgeWidth, 0.001f)", StringComparison.Ordinal) &&
        !capsuleSource.Contains("fwidth(", StringComparison.Ordinal),
        "落下水筋は呼出元の解析的輪郭幅を使用する必要があります。");
    True(
        mappedTrailSource.Contains(
            "lerp(startInputUv, endInputUv, trailProgress)",
            StringComparison.Ordinal) &&
        mappedTrailSource.Contains(
            "GetOutsideDropletRegionPixelsPerInputPixel(",
            StringComparison.Ordinal),
        "面変形する水筋は現在の進捗位置における入力1px換算を使用する必要があります。");
    True(
        regionPixelStepSource.Contains(
            "gradientU.x * regionPixelSize.x",
            StringComparison.Ordinal) &&
        regionPixelStepSource.Contains(
            "gradientV.x * regionPixelSize.y",
            StringComparison.Ordinal) &&
        regionPixelStepSource.Contains(
            "gradientU.y * regionPixelSize.x",
            StringComparison.Ordinal) &&
        regionPixelStepSource.Contains(
            "gradientV.y * regionPixelSize.y",
            StringComparison.Ordinal) &&
        regionPixelStepSource.Contains(
            "abs(denominator) < 0.000001f",
            StringComparison.Ordinal) &&
        regionPixelStepSource.Contains("return 1.0f;", StringComparison.Ordinal),
        "Quadの入力X/Y微分を対応する領域軸へ組み立て、不正分母では1pxへ戻す必要があります。");

    NearlyEqual(
        1,
        ReferenceFallingDropletWidthScale(-0.6f, 0),
        "落下開始時の上側幅",
        0.000001f);
    NearlyEqual(
        1,
        ReferenceFallingDropletWidthScale(0.6f, 0),
        "落下開始時の下側幅",
        0.000001f);
    var upperWidth = ReferenceFallingDropletWidthScale(-0.6f, 1);
    var lowerWidth = ReferenceFallingDropletWidthScale(0.6f, 1);
    True(
        upperWidth < lowerWidth,
        "落下中は上側が下側より細い必要があります。");
    // 添付の輪郭を高さで正規化した幅。色やハイライトは比較対象にしない。
    var referenceWidths = new (float Height, float Width)[]
    {
        (0.05f, 0.100f), (0.10f, 0.147f), (0.20f, 0.287f),
        (0.30f, 0.433f), (0.40f, 0.611f), (0.50f, 0.783f),
        (0.60f, 0.923f), (0.70f, 0.992f), (0.75f, 1.000f),
        (0.80f, 0.977f), (0.90f, 0.817f), (0.95f, 0.631f),
    };
    var sampledWidths = Enumerable.Range(0, 1001)
        .Select(index => (
            Height: index / 1000f,
            Width: ReferenceFallingDropletHalfWidth(index / 500f - 1, 1)))
        .ToArray();
    var widest = sampledWidths.MaxBy(sample => sample.Width);
    True(
        widest.Height > 0.72f && widest.Height < 0.77f,
        "添付形状と同じく、最も太い位置は上端から約3/4に置く必要があります。");
    foreach (var (height, width) in referenceWidths)
    {
        NearlyEqual(
            width,
            ReferenceFallingDropletHalfWidth(height * 2 - 1, 1) / widest.Width,
            $"参照輪郭からの幅のずれ 高さ={height}",
            0.06f);
    }
    var finalAspectRatio = widest.Width / 2.2f;
    True(
        finalAspectRatio > 0.43f && finalAspectRatio < 0.47f &&
        fallingSource.Contains("2.20f", StringComparison.Ordinal),
        "落下時の縦長比率は参照画像の幅/高さ約0.45に合わせる必要があります。");
    foreach (var sample in sampledWidths)
    {
        True(float.IsFinite(sample.Width), "先端から下端まで幅は有限である必要があります。");
        var y = sample.Height * 2 - 1;
        NearlyEqual(
            MathF.Sqrt(MathF.Max(1 - y * y, 0)),
            ReferenceFallingDropletHalfWidth(y, 0),
            "静止中は元の円形断面を維持する必要があります。",
            0.000001f);
    }

    var upperDistance = ReferenceFallingDropletDistance(
        new Vector2(0.55f, -0.6f),
        1);
    var lowerDistance = ReferenceFallingDropletDistance(
        new Vector2(0.55f, 0.6f),
        1);
    True(
        upperDistance > 1 && lowerDistance < 1,
        "同じ横距離で上側は被覆外、下側は被覆内となる水滴型が必要です。");
    NearlyEqual(
        lowerDistance,
        ReferenceFallingDropletDistance(new Vector2(-0.55f, 0.6f), 1),
        "水滴型の左右対称",
        0.000001f);

    NearlyEqual(
        100,
        ReferenceOutsideDropletUpperAnchorY(100, 12, 1.4f, 0),
        "落下開始時の水筋接続",
        0.000001f);
    True(
        ReferenceOutsideDropletUpperAnchorY(100, 12, 1.4f, 1) < 100,
        "落下中の水筋終端は頭部中心より上へ接続する必要があります。");

    NearlyEqual(
        1,
        ReferenceOutsideDropletRegionPixelsPerInputPixel(
            GlassWipeHomography.Identity,
            new Vector2(0.5f, 0.5f),
            new Vector2(1920, 1080),
            new Vector2(1920, 1080)),
        "恒等Quadでは入力1pxを領域1pxとして扱う必要があります。",
        0.000001f);
    NearlyEqual(
        1.8f,
        ReferenceOutsideDropletRegionPixelsPerInputPixel(
            new GlassWipeHomography(
                2, 0, 0,
                0, 3, 0,
                0, 0, 1),
            new Vector2(0.25f, 0.75f),
            new Vector2(1000, 500),
            new Vector2(200, 300)),
        "QuadのX/Y微分を対応する領域軸へ組み立てる必要があります。",
        0.000001f);
    NearlyEqual(
        1,
        ReferenceOutsideDropletRegionPixelsPerInputPixel(
            new GlassWipeHomography(
                1, 0, 0,
                0, 1, 0,
                0, 0, 0),
            Vector2.Zero,
            new Vector2(1920, 1080),
            new Vector2(1920, 1080)),
        "Quad逆射影の分母が不正な場合は1pxへ安全に戻す必要があります。",
        0.000001f);
    NearlyEqual(
        1,
        ReferenceOutsideDropletRegionPixelsPerInputPixel(
            new GlassWipeHomography(
                -1, 0, 0,
                0, -1, 0,
                0, 0, -1),
            new Vector2(0.5f, 0.5f),
            new Vector2(1920, 1080),
            new Vector2(1920, 1080)),
        "負の射影分母は絶対値が有効なら通常の1px換算を行う必要があります。",
        0.000001f);

    var perspectiveQuad = new GlassWipeQuad(
        new Vector2(0.08f, 0.18f),
        new Vector2(0.92f, 0.08f),
        new Vector2(0.78f, 0.94f),
        new Vector2(0.22f, 0.82f));
    True(
        perspectiveQuad.TryCreateMapping(out var perspectiveMapping),
        "解析的輪郭幅試験用の透視Quadを生成できません。");
    var perspectiveInputSize = new Vector2(1920, 1080);
    var perspectiveRegionSize = perspectiveQuad.GetNominalPixelSize(
        perspectiveInputSize.X,
        perspectiveInputSize.Y);
    foreach (var localPosition in new[]
             {
                 new Vector2(0.2f, 0.25f),
                 new Vector2(0.5f, 0.5f),
                 new Vector2(0.8f, 0.75f),
             })
    {
        var inputUv = perspectiveMapping.LocalToInput.Transform(localPosition);
        var analytic = ReferenceOutsideDropletRegionPixelsPerInputPixel(
            perspectiveMapping.InputToLocal,
            inputUv,
            perspectiveInputSize,
            perspectiveRegionSize);
        var finiteDifference = ReferenceOutsideDropletRegionPixelsPerInputPixelFiniteDifference(
            perspectiveMapping.InputToLocal,
            inputUv,
            perspectiveInputSize,
            perspectiveRegionSize);
        NearlyEqual(
            finiteDifference,
            analytic,
            $"透視Quadの解析的1px換算 local={localPosition}",
            0.001f);
    }
    var upperBoundaryY = -0.6f;
    var upperBoundaryX = ReferenceFallingDropletHalfWidth(upperBoundaryY, 1);
    var onePixelEdge = ReferenceFallingDropletEdgeWidth(
        new Vector2(upperBoundaryX, upperBoundaryY),
        1,
        20,
        2.2f,
        1);
    var twoPixelEdge = ReferenceFallingDropletEdgeWidth(
        new Vector2(upperBoundaryX, upperBoundaryY),
        1,
        20,
        2.2f,
        2);
    True(
        float.IsFinite(onePixelEdge) && onePixelEdge > 0,
        "落下headの解析的輪郭幅は有限の正値である必要があります。");
    NearlyEqual(
        onePixelEdge * 2,
        twoPixelEdge,
        "入力pixel換算が2倍なら正規化輪郭幅も2倍になる必要があります。",
        0.000001f);

    foreach (var progress in new[] { 0f, 0.15f, 0.25f, 0.35f, 1f })
    {
        foreach (var y in new[] { -0.98f, -0.9f, -0.6f, 0f, 0.5f, 0.95f })
        {
            var boundary = new Vector2(ReferenceFallingDropletHalfWidth(y, progress), y);
            const float delta = 0.0001f;
            var derivativeX = (
                ReferenceFallingDropletDistance(boundary + new Vector2(delta, 0), progress) -
                ReferenceFallingDropletDistance(boundary - new Vector2(delta, 0), progress)) /
                (2 * delta * 20);
            var derivativeY = (
                ReferenceFallingDropletDistance(boundary + new Vector2(0, delta), progress) -
                ReferenceFallingDropletDistance(boundary - new Vector2(0, delta), progress)) /
                (2 * delta * 20 * 2.2f);
            NearlyEqual(
                new Vector2(derivativeX, derivativeY).Length(),
                ReferenceFallingDropletEdgeWidth(boundary, progress, 20, 2.2f, 1),
                $"輪郭勾配と有限差分の一致 y={y}, progress={progress}",
                0.0003f);
        }
    }
    foreach (var radius in new[] { 2f, 8f, 24f, 70f })
    {
        foreach (var x in new[] { 0f, 0.5f, 1f, 2f, 4f })
        {
            var point = new Vector2(x / radius, -1 - 3 / (radius * 2.2f));
            var distance = ReferenceFallingDropletDistance(point, 1);
            var edge = ReferenceFallingDropletEdgeWidth(point, 1, radius, 2.2f, 1);
            var axialCoverage = 1 - SmoothStepReference(
                1, 1 + 1 / (radius * 2.2f), MathF.Abs(point.Y));
            var coverage = (1 - SmoothStepReference(1 - edge, 1 + edge, distance)) * axialCoverage;
            True(float.IsFinite(edge), "極小の滴でも輪郭幅は有限である必要があります。");
            NearlyEqual(0, coverage, "先端の3px上へ余計な縦線を伸ばしてはいけません。", 0.000001f);
        }
    }
}

static void VerifyOutsideDropletMotionDeterminism()
{
    foreach (var request in new[]
             {
                 (Frame: -1, Fps: 0),
                 (Frame: 0, Fps: 60),
                 (Frame: 1, Fps: 24),
                 (Frame: 120, Fps: 60),
                 (Frame: 120, Fps: 24),
                 (Frame: int.MaxValue, Fps: 60),
             })
    {
        NearlyEqual(
            ReferenceOutsideDropletLocalTimeSeconds(request.Frame, request.Fps),
            OutsideDropletMotion.GetLocalTimeSeconds(request.Frame, request.Fps),
            $"独立式とのローカル時刻一致 frame={request.Frame}, fps={request.Fps}",
            0.000001f);
    }
    NearlyEqual(6, OutsideDropletMotion.GetFallDurationSeconds(1, 1), "速度100%の領域高さ移動時間", 0.000001f);
    NearlyEqual(24, OutsideDropletMotion.GetFallDurationSeconds(0.25f, 1), "速度25%の領域高さ移動時間", 0.000001f);
    NearlyEqual(1.5f, OutsideDropletMotion.GetFallDurationSeconds(4, 1), "速度400%の領域高さ移動時間", 0.000001f);
    True(
        OutsideDropletMotion.GetFallDurationSeconds(1, 1.15f) <
        OutsideDropletMotion.GetFallDurationSeconds(1, 0.9f),
        "大きな水滴を小さな水滴より少し速くする必要があります。");

    var requests = new List<(int Frame, int Fps, uint EmitterKey, float Speed, float Size)>();
    var random = new Random(1200);
    for (var index = 0; index < 1000; index++)
    {
        var seed = (uint)random.Next(0, 10000);
        var layer = new uint[] { 101, 307, 701 }[index % 3];
        requests.Add((
            random.Next(0, 60 * 60 * 30),
            new[] { 24, 30, 60 }[index % 3],
            OutsideDropletRandom.GetEmitterKey(seed, layer, (uint)(index % 6)),
            new[] { 0.25f, 1, 4 }[index % 3],
            new[] { 0.9f, 1, 1.15f }[index % 3]));
    }

    var expected = requests
        .Select(request => EvaluateOutsideDropletHlslReference(
            ReferenceOutsideDropletLocalTimeSeconds(request.Frame, request.Fps),
            request.EmitterKey,
            request.Speed,
            request.Size))
        .ToArray();
    for (var index = requests.Count - 1; index >= 0; index--)
    {
        var request = requests[index];
        var actual = OutsideDropletMotion.EvaluateLifecycle(
            OutsideDropletMotion.GetLocalTimeSeconds(request.Frame, request.Fps),
            request.EmitterKey,
            request.Speed,
            request.Size);
        OutsideDropletLifecycleEqual(
            expected[index],
            actual,
            $"HLSL参照との前後シーク決定性 index={index}");
    }

    var boundaryEmitterKey = OutsideDropletRandom.GetEmitterKey(701, 701, 3);
    var referenceTimings = GetOutsideDropletReferenceTimings(
        boundaryEmitterKey,
        1,
        1.15f);
    var boundaryTime = referenceTimings.CycleDuration * 4 -
        referenceTimings.PhaseOffset;
    var visibleWaitDuration = referenceTimings.VisibleWaitDuration;
    var fallDuration = referenceTimings.FallDuration;
    var waiting = OutsideDropletMotion.EvaluateLifecycle(
        boundaryTime + visibleWaitDuration * 0.5f,
        boundaryEmitterKey,
        1,
        1.15f);
    var falling = OutsideDropletMotion.EvaluateLifecycle(
        boundaryTime + visibleWaitDuration + fallDuration * 0.5f,
        boundaryEmitterKey,
        1,
        1.15f);
    var hidden = OutsideDropletMotion.EvaluateLifecycle(
        boundaryTime + visibleWaitDuration + fallDuration + 0.001f,
        boundaryEmitterKey,
        1,
        1.15f);
    Equal(OutsideDropletPhase.WaitingAtStart, waiting.Phase, "周期先頭は開始位置で待機する必要があります。");
    True(waiting.Visibility > 0.99f, "開始位置の待機中は水滴を表示する必要があります。");
    Equal(OutsideDropletPhase.Falling, falling.Phase, "待機後は落下へ移る必要があります。");
    NearlyEqual(0.5f, falling.Progress, "落下中間の進行度", 0.001f);
    Equal(OutsideDropletPhase.Hidden, hidden.Phase, "領域端で消えた後は非表示待機へ移る必要があります。");
    NearlyEqual(0, hidden.Visibility, "非表示待機中の可視度", 0.000001f);

    var before = OutsideDropletMotion.EvaluateLifecycle(
        boundaryTime - 0.0001f,
        boundaryEmitterKey,
        1,
        1.15f);
    var atBoundary = OutsideDropletMotion.EvaluateLifecycle(
        boundaryTime,
        boundaryEmitterKey,
        1,
        1.15f);
    var after = OutsideDropletMotion.EvaluateLifecycle(
        boundaryTime + 0.0001f,
        boundaryEmitterKey,
        1,
        1.15f);
    NearlyEqual(0, before.Visibility, "ループ境界直前のフェードアウト", 0.001f);
    NearlyEqual(0, atBoundary.Visibility, "ループ境界のフェード接続", 0.001f);
    NearlyEqual(0, after.Visibility, "ループ境界直後のフェードイン開始", 0.001f);
    True(atBoundary.CycleIndex >= before.CycleIndex, "ループ境界で周期番号が逆行してはいけません。");
}

static void VerifyOutsideDropletFixedEmitterReferenceContract()
{
    var shaderSource = RemoveWhitespace(ShaderVerificationSource.ReadAllText(
        Path.Combine(
            FindRepositoryRoot(),
            "src",
            "YMM4GlassWipe",
            "Shaders",
            "GlassComposite.hlsl")));
    True(
        shaderSource.Contains(
            "staticconstintOutsideDropletBaseEmitterCount=6;",
            StringComparison.Ordinal) &&
        shaderSource.Contains(
            "uintemitterKey=GetOutsideDropletEmitterKey((uint)outsideDropletSeed,layer,emitterIndex);",
            StringComparison.Ordinal) &&
        shaderSource.Contains(
            "uintcycleKey=GetOutsideDropletCycleKey(emitterKey,(uint)lifecycle.z);",
            StringComparison.Ordinal),
        "基準6個体と個体/cycle整数キー式をHLSLへ維持する必要があります。");
    foreach (var layerContract in new[]
             {
                 "101u,0.75f,0.9f,isQuad",
                 "307u,1.0f,1.0f,isQuad",
                 "701u,1.25f,1.15f,isQuad",
             })
    {
        True(
            shaderSource.Contains(layerContract, StringComparison.Ordinal),
            $"固定落下層の契約が不足しています: {layerContract}");
    }

    var layers = new uint[] { 101, 307, 701 };
    foreach (var seed in new uint[] { 0, 9999 })
    {
        var forward = new List<uint>();
        foreach (var layer in layers)
        {
            var emitterKeys = Enumerable.Range(0, 6)
                .Select(index => OutsideDropletRandom.GetEmitterKey(seed, layer, (uint)index))
                .ToArray();
            Equal(6, emitterKeys.Distinct().Count(), $"Seed {seed}の層{layer}で個体キーを重複させてはいけません。");
            forward.AddRange(emitterKeys);
            foreach (var emitterKey in emitterKeys)
            {
                foreach (var cycleIndex in new uint[] { 0, 1, 17, uint.MaxValue })
                {
                    var first = OutsideDropletRandom.GetCycleKey(emitterKey, cycleIndex);
                    var repeated = OutsideDropletRandom.GetCycleKey(emitterKey, cycleIndex);
                    Equal(first, repeated, "同じ個体/cycleの整数キー再現性");
                    if (cycleIndex > 0)
                    {
                        True(
                            first != emitterKey,
                            "cycleが変わると再配置キーも変わる必要があります。");
                    }
                }
            }
        }

        Equal(18, forward.Count, $"Seed {seed}は3層×6個体である必要があります。");
        var reverse = layers
            .Reverse()
            .SelectMany(layerOffset => Enumerable.Range(0, 6)
                .Reverse()
                .Select(index => OutsideDropletRandom.GetEmitterKey(seed, layerOffset, (uint)index)))
            .Reverse()
            .ToArray();
        for (var index = 0; index < forward.Count; index++)
        {
            Equal(
                forward[index],
                reverse[index],
                $"列挙順に依存しない個体キー index={index}");
        }
    }
}

static void VerifyOutsideDropletIntegerRandomContract()
{
    // 次の固定値はOutsideDropletRandomとは別のPython実装で算出したgolden vectorです。
    foreach (var vector in new[]
             {
                 (Input: 0U, Expected: 0x00000000U),
                 (Input: 1U, Expected: 0x688990c0U),
                 (Input: uint.MaxValue, Expected: 0x6768824aU),
                 (Input: 0x12345678U, Expected: 0xf5e71c96U),
             })
    {
        Equal(vector.Expected, OutsideDropletRandom.Mix(vector.Input), "Mix golden vector");
    }

    foreach (var vector in new[]
             {
                 (CellX: 0, CellY: 0, Seed: 0U, Layer: 101U, Expected: 0x0504b27dU),
                 (CellX: -1, CellY: 2, Seed: 0U, Layer: 307U, Expected: 0x76b8400fU),
                 (CellX: 123, CellY: -456, Seed: 9999U, Layer: 701U, Expected: 0xd8a250b6U),
             })
    {
        Equal(
            vector.Expected,
            OutsideDropletRandom.GetCellKey(
                vector.CellX,
                vector.CellY,
                vector.Seed,
                vector.Layer),
            "負セルを含むcell key golden vector");
    }

    foreach (var vector in new[]
             {
                 (Seed: 0U, Layer: 101U, Emitter: 0U, Expected: 0x7c8f9fd6U),
                 (Seed: 0U, Layer: 307U, Emitter: 5U, Expected: 0x659a20ebU),
                 (Seed: 9999U, Layer: 701U, Emitter: 5U, Expected: 0x25746a89U),
             })
    {
        Equal(
            vector.Expected,
            OutsideDropletRandom.GetEmitterKey(
                vector.Seed,
                vector.Layer,
                vector.Emitter),
            "emitter key golden vector");
    }

    foreach (var vector in new[]
             {
                 (EmitterKey: 0x7c8f9fd6U, Cycle: 0U, Expected: 0xca84faf3U),
                 (EmitterKey: 0x659a20ebU, Cycle: 17U, Expected: 0xed0f0ff2U),
                 (EmitterKey: 0x25746a89U, Cycle: uint.MaxValue, Expected: 0xb06d74b8U),
             })
    {
        Equal(
            vector.Expected,
            OutsideDropletRandom.GetCycleKey(vector.EmitterKey, vector.Cycle),
            "cycle key golden vector");
    }

    foreach (var vector in new[]
             {
                 (Key: 0x0504b27dU, Channel: 0U, Expected: 0.07882630825042725f),
                 (Key: 0x76b8400fU, Channel: 4U, Expected: 0.5594493746757507f),
                 (Key: 0x25746a89U, Channel: 5U, Expected: 0.5793167352676392f),
                 (Key: 0x25746a89U, Channel: 6U, Expected: 0.2475789189338684f),
                 (Key: 0x25746a89U, Channel: 7U, Expected: 0.19633013010025024f),
                 (Key: 0x6f4b4320U, Channel: 8U, Expected: 0.002391815185546875f),
                 (Key: 0x01fce552U, Channel: 0U, Expected: 0f),
                 (Key: 0xd4050a96U, Channel: 0U, Expected: 0.9999999403953552f),
             })
    {
        Equal(
            vector.Expected,
            OutsideDropletRandom.Sample(vector.Key, vector.Channel),
            "固定channel sample golden vector");
    }

    var cells = new[]
    {
        (CellX: -3, CellY: -2),
        (CellX: -2, CellY: 1),
        (CellX: -1, CellY: 4),
        (CellX: 0, CellY: -4),
        (CellX: 1, CellY: -1),
        (CellX: 2, CellY: 2),
    };
    foreach (var seed in new uint[] { 0, 9999 })
    {
        foreach (var layer in new uint[] { 101, 307, 701 })
        {
            foreach (var cell in cells)
            {
                var key = OutsideDropletRandom.GetCellKey(cell.CellX, cell.CellY, seed, layer);
                var samples = Enumerable.Range(0, 9)
                    .Select(channel => OutsideDropletRandom.Sample(key, (uint)channel))
                    .ToArray();
                True(
                    samples.All(value => float.IsFinite(value) && value is >= 0 and < 1),
                    $"Seed {seed}、層{layer}、cell({cell.CellX},{cell.CellY})の乱数は[0, 1)である必要があります。");
                Equal(9, samples.Distinct().Count(), "この固定cellケースでは乱数channelの値を分離する必要があります。");

                var centerX = 0.25f + 0.5f * samples[1];
                var centerY = 0.25f + 0.5f * samples[2];
                var radius = 0.11f + 0.10f * samples[3];
                var verticalScale = 0.88f + 0.30f * samples[4];
                True(
                    centerX is >= 0.25f and < 0.75f &&
                    centerY is >= 0.25f and < 0.75f &&
                    radius is >= 0.11f and < 0.21f &&
                    verticalScale is >= 0.88f and < 1.18f,
                    "静止滴の中心、半径、縦倍率は乱数から定義された範囲内である必要があります。");
            }
        }
    }

    var forward = cells
        .Select(cell => OutsideDropletRandom.GetCellKey(cell.CellX, cell.CellY, 9999, 307))
        .ToArray();
    var reverse = cells
        .Reverse()
        .Select(cell => OutsideDropletRandom.GetCellKey(cell.CellX, cell.CellY, 9999, 307))
        .Reverse()
        .ToArray();
    for (var index = 0; index < forward.Length; index++)
    {
        Equal(forward[index], reverse[index], $"再実行・逆順一致 index={index}");
    }

    var shaderSource = ShaderVerificationSource.ReadAllText(Path.Combine(
        FindRepositoryRoot(),
        "src",
        "YMM4GlassWipe",
        "Shaders",
        "GlassComposite.hlsl"));
    foreach (var contract in new[]
             {
                 (
                     Signature: "uint MixOutsideDropletBits(",
                     Expected: "uintMixOutsideDropletBits(uintvalue){value^=value>>16;value*=0x7feb352du;value^=value>>15;value*=0x846ca68bu;value^=value>>16;returnvalue;}"),
                 (
                     Signature: "uint GetOutsideDropletCellKey(",
                     Expected: "uintGetOutsideDropletCellKey(int2cell,uintseed,uintlayer){uintkey=MixOutsideDropletBits(seed^0xa511e9b3u);key=MixOutsideDropletBits(key^(uint)cell.x);key=MixOutsideDropletBits(key^(uint)cell.y);returnMixOutsideDropletBits(key^layer);}"),
                 (
                     Signature: "uint GetOutsideDropletEmitterKey(",
                     Expected: "uintGetOutsideDropletEmitterKey(uintseed,uintlayer,uintemitterIndex){uintkey=MixOutsideDropletBits(seed^0x63d83595u);key=MixOutsideDropletBits(key^layer);returnMixOutsideDropletBits(key^emitterIndex);}"),
                 (
                     Signature: "uint GetOutsideDropletCycleKey(",
                     Expected: "uintGetOutsideDropletCycleKey(uintemitterKey,uintcycleIndex){returnMixOutsideDropletBits(emitterKey^MixOutsideDropletBits(cycleIndex^0xb5297a4du));}"),
                 (
                     Signature: "float OutsideDropletRandom(",
                     Expected: "floatOutsideDropletRandom(uintkey,uintchannel){uintbits=MixOutsideDropletBits(key^MixOutsideDropletBits(channel+0x9e3779b9u));return(float)(bits>>8)*(1.0f/16777216.0f);}"),
             })
    {
        Equal(
            contract.Expected,
            RemoveWhitespace(ExtractHlslFunction(shaderSource, contract.Signature)),
            $"HLSL整数乱数本文の固定契約: {contract.Signature}");
    }

    var staticLayerSource = RemoveWhitespace(ExtractHlslFunction(
        shaderSource,
        "float3 EvaluateOutsideDropletLayer("));
    True(
        staticLayerSource.Contains(
            "int2cell=(int2)floor(cellPosition);",
            StringComparison.Ordinal) &&
        staticLayerSource.Contains(
            "uintcellKey=GetOutsideDropletCellKey(cell,(uint)outsideDropletSeed,layer);",
            StringComparison.Ordinal) &&
        Enumerable.Range(0, 5).All(channel => staticLayerSource.Contains(
            $"OutsideDropletRandom(cellKey,{channel}u)",
            StringComparison.Ordinal)),
        "静止滴は整数cell keyからchannel 0から4を使用する必要があります。");

    var lifecycleSource = RemoveWhitespace(ExtractHlslFunction(
        shaderSource,
        "float4 EvaluateOutsideDropletLifecycle("));
    True(
        Enumerable.Range(5, 3).All(channel => lifecycleSource.Contains(
            $"OutsideDropletRandom(emitterKey,{channel}u)",
            StringComparison.Ordinal)),
        "Lifecycleはemitter keyからchannel 5から7を使用する必要があります。");

    var emitterSource = RemoveWhitespace(ExtractHlslFunction(
        shaderSource,
        "float3 EvaluateFallingOutsideDropletEmitter("));
    True(
        emitterSource.Contains(
            "uintemitterKey=GetOutsideDropletEmitterKey((uint)outsideDropletSeed,layer,emitterIndex);",
            StringComparison.Ordinal) &&
        emitterSource.Contains("OutsideDropletRandom(emitterKey,0u)", StringComparison.Ordinal) &&
        emitterSource.Contains("OutsideDropletRandom(emitterKey,8u)", StringComparison.Ordinal) &&
        emitterSource.Contains(
            "uintcycleKey=GetOutsideDropletCycleKey(emitterKey,(uint)lifecycle.z);",
            StringComparison.Ordinal) &&
        emitterSource.Contains("GetOutsideDropletSpawnRegionUv(cycleKey)", StringComparison.Ordinal) &&
        emitterSource.Contains("OutsideDropletRandom(cycleKey,3u)", StringComparison.Ordinal) &&
        emitterSource.Contains("OutsideDropletRandom(cycleKey,4u)", StringComparison.Ordinal),
        "落下滴はemitter/cycle keyとchannel 0、8、3、4を用途どおり接続する必要があります。");

    var spawnSource = RemoveWhitespace(ExtractHlslFunction(
        shaderSource,
        "float2 GetOutsideDropletSpawnRegionUv("));
    True(
        spawnSource.Contains("OutsideDropletRandom(cycleKey,1u)", StringComparison.Ordinal) &&
        spawnSource.Contains("OutsideDropletRandom(cycleKey,2u)", StringComparison.Ordinal) &&
        spawnSource.Contains("cos(angle)", StringComparison.Ordinal) &&
        spawnSource.Contains("sin(angle)", StringComparison.Ordinal),
        "spawnはcycle keyのchannel 1と2を使い、楕円領域のgeometry sin/cosだけを使用する必要があります。");
    foreach (var forbidden in new[] { "HashNoise2D(", "HashScalar(", "asuint(" })
    {
        True(
            !spawnSource.Contains(forbidden, StringComparison.OrdinalIgnoreCase),
            $"spawnは{forbidden}へ依存してはいけません。");
    }

    var productionRandomSource = string.Concat(
        ExtractHlslFunction(shaderSource, "uint MixOutsideDropletBits("),
        ExtractHlslFunction(shaderSource, "uint GetOutsideDropletCellKey("),
        ExtractHlslFunction(shaderSource, "uint GetOutsideDropletEmitterKey("),
        ExtractHlslFunction(shaderSource, "uint GetOutsideDropletCycleKey("),
        ExtractHlslFunction(shaderSource, "float OutsideDropletRandom("),
        ExtractHlslFunction(shaderSource, "float3 EvaluateOutsideDropletLayer("),
        ExtractHlslFunction(shaderSource, "float4 EvaluateOutsideDropletLifecycle("),
        ExtractHlslFunction(shaderSource, "float3 EvaluateFallingOutsideDropletEmitter("),
        ExtractHlslFunction(shaderSource, "float3 GetOutsideDropletLighting("));
    foreach (var forbidden in new[] { "HashNoise2D(", "HashScalar(", "sin(", "asuint(" })
    {
        True(
            !productionRandomSource.Contains(forbidden, StringComparison.OrdinalIgnoreCase),
            $"本番水滴乱数は{forbidden}へ依存してはいけません。");
    }
    True(
        shaderSource.Contains("float HashNoise2D(", StringComparison.Ordinal) &&
        shaderSource.Contains("GetOutsideDropletCellStateFingerprintWithoutSine", StringComparison.Ordinal),
        "くもり・拭きムラ用HashNoise2Dと旧診断は維持する必要があります。");
}

static void VerifyRegionShapeEnum()
{
    Equal(0, (int)GlassWipeRegionShape.FullScreen, "全画面の値が不正です。");
    Equal(1, (int)GlassWipeRegionShape.Rectangle, "四角形の値が不正です。");
    Equal(2, (int)GlassWipeRegionShape.Ellipse, "円・楕円の値が不正です。");
    Equal(3, (int)GlassWipeRegionShape.Quad, "四隅指定の値が不正です。");
    Equal("入力領域全体", GetDisplayName(GlassWipeRegionShape.FullScreen), "入力領域全体の表示名が不正です。");
    Equal("入力領域全体", GetDisplayName(GlassWipeSimpleRegionShape.FullScreen), "かんたん作成の入力領域全体の表示名が不正です。");
    Equal("四角形（四隅指定）", GetDisplayName(GlassWipeRegionShape.Quad), "四隅指定の表示名が不正です。");
    Equal("四角形（四隅指定）", GetDisplayName(GlassWipeSimpleRegionShape.Quad), "標準モードの四隅指定の表示名が不正です。");
    Equal(4, Enum.GetValues<GlassWipeRegionShape>().Length, "Region形状の要素数が不正です。");
    Equal(
        GlassWipeRegionShape.Ellipse,
        GlassWipeParameterSanitizer.SanitizeRegionShape(
            GlassWipeRegionShape.Ellipse),
        "円・楕円のRegion形状を保持する必要があります。");
    Equal(
        GlassWipeRegionShape.Quad,
        GlassWipeParameterSanitizer.SanitizeRegionShape(
            GlassWipeRegionShape.Quad),
        "四隅指定のRegion形状を保持する必要があります。");
    Equal(
        GlassWipeRegionShape.Rectangle,
        GlassWipeParameterSanitizer.SanitizeRegionShape(
            (GlassWipeRegionShape)99),
        "未定義のRegion形状は四角形へ補正する必要があります。");
}

static void VerifyQuadPresetEnum()
{
    Equal(0, (int)GlassWipeQuadPreset.Custom, "カスタムQuadプリセットの値が不正です。");
    Equal(1, (int)GlassWipeQuadPreset.FrontWindshield, "フロントガラスプリセットの値が不正です。");
    Equal(2, Enum.GetValues<GlassWipeQuadPreset>().Length, "Quadプリセットの要素数が不正です。");
    Equal(
        GlassWipeQuadPreset.FrontWindshield,
        GlassWipeParameterSanitizer.SanitizeQuadPreset(
            GlassWipeQuadPreset.FrontWindshield),
        "フロントガラスプリセットを保持する必要があります。");
    Equal(
        GlassWipeQuadPreset.Custom,
        GlassWipeParameterSanitizer.SanitizeQuadPreset(
            (GlassWipeQuadPreset)99),
        "未定義のQuadプリセットはカスタムへ補正する必要があります。");

    var custom = new GlassWipeQuad(
        new Vector2(0.1f, 0.2f),
        new Vector2(0.9f, 0.2f),
        new Vector2(0.9f, 0.8f),
        new Vector2(0.1f, 0.8f));
    Equal(custom, custom.ResolvePreset(GlassWipeQuadPreset.Custom), "カスタムQuadが変化しました。");
    Equal(
        GlassWipeQuad.FrontWindshield,
        custom.ResolvePreset(GlassWipeQuadPreset.FrontWindshield),
        "フロントガラスプリセットが適用されていません。");
}

static void VerifyQuadHomography()
{
    var quad = new GlassWipeQuad(
        new Vector2(0.15f, 0.12f),
        new Vector2(0.82f, 0.18f),
        new Vector2(0.93f, 0.88f),
        new Vector2(0.08f, 0.79f));
    True(quad.TryCreateMapping(out var mapping), "正常な凸QuadのHomographyを生成できません。");
    True(mapping.IsValid, "正常な凸Quadが有効として記録されていません。");

    var corners = new[]
    {
        (Vector2.Zero, quad.TopLeft),
        (Vector2.UnitX, quad.TopRight),
        (Vector2.One, quad.BottomRight),
        (Vector2.UnitY, quad.BottomLeft),
    };
    foreach (var (local, expectedInput) in corners)
    {
        Vector2Equal(
            expectedInput,
            mapping.LocalToInput.Transform(local),
            $"Quad四隅が一致しません。Local={local}",
            0.00001f);
    }

    foreach (var expectedLocal in new[]
    {
        new Vector2(0.2f, 0.3f),
        new Vector2(0.5f, 0.5f),
        new Vector2(0.8f, 0.7f),
    })
    {
        var input = mapping.LocalToInput.Transform(expectedLocal);
        var actualLocal = mapping.InputToLocal.Transform(input);
        Vector2Equal(
            expectedLocal,
            actualLocal,
            $"Homographyの往復が一致しません。Local={expectedLocal}",
            0.00001f);
    }
}

static void VerifyInvalidQuad()
{
    var cases = new[]
    {
        (
            "重複点",
            new GlassWipeQuad(
                new Vector2(0.1f, 0.1f),
                new Vector2(0.1f, 0.1f),
                new Vector2(0.9f, 0.9f),
                new Vector2(0.1f, 0.9f))),
        (
            "自己交差",
            new GlassWipeQuad(
                new Vector2(0.1f, 0.1f),
                new Vector2(0.9f, 0.9f),
                new Vector2(0.9f, 0.1f),
                new Vector2(0.1f, 0.9f))),
        (
            "退化",
            new GlassWipeQuad(
                new Vector2(0.1f, 0.1f),
                new Vector2(0.4f, 0.1f),
                new Vector2(0.7f, 0.1f),
                new Vector2(0.9f, 0.1f))),
        (
            "逆順",
            new GlassWipeQuad(
                new Vector2(0.1f, 0.1f),
                new Vector2(0.1f, 0.9f),
                new Vector2(0.9f, 0.9f),
                new Vector2(0.9f, 0.1f))),
        (
            "非有限値",
            new GlassWipeQuad(
                new Vector2(float.NaN, 0.1f),
                new Vector2(0.9f, 0.1f),
                new Vector2(0.9f, 0.9f),
                new Vector2(0.1f, 0.9f))),
    };

    foreach (var (name, quad) in cases)
    {
        var created = quad.TryCreateMapping(out var mapping);
        True(!created && !mapping.IsValid, $"{name}のQuadを有効として扱ってはいけません。");
    }
}

static void VerifyQuadSizeAndHistoryFollow()
{
    var rectangle = new GlassWipeQuad(
        new Vector2(0.1f, 0.2f),
        new Vector2(0.9f, 0.2f),
        new Vector2(0.9f, 0.8f),
        new Vector2(0.1f, 0.8f));
    var size = rectangle.GetNominalPixelSize(1000, 500);
    Vector2Equal(new Vector2(800, 300), size, "Quadの公称ピクセル寸法が不正です。");

    var moved = new GlassWipeQuad(
        new Vector2(0.2f, 0.08f),
        new Vector2(0.88f, 0.22f),
        new Vector2(0.78f, 0.92f),
        new Vector2(0.05f, 0.68f));
    True(rectangle.TryCreateMapping(out var first), "履歴追従試験の初期Quadが不正です。");
    True(moved.TryCreateMapping(out var current), "履歴追従試験の現在Quadが不正です。");

    var storedLocalPoint = new Vector2(0.35f, 0.65f);
    var firstInput = first.LocalToInput.Transform(storedLocalPoint);
    var currentInput = current.LocalToInput.Transform(storedLocalPoint);
    True(
        Vector2.Distance(firstInput, currentInput) > 0.05f,
        "Quad変更後も履歴点の入力位置が変化していません。");
    Vector2Equal(
        storedLocalPoint,
        current.InputToLocal.Transform(currentInput),
        "ローカル履歴点が現在Quadへ追従していません。",
        0.00001f);
}

static void VerifyQuadFeather()
{
    var quad = new GlassWipeQuad(
        new Vector2(0.1f, 0.2f),
        new Vector2(0.9f, 0.2f),
        new Vector2(0.9f, 0.8f),
        new Vector2(0.1f, 0.8f));
    True(quad.TryCreateMapping(out var mapping), "境界ぼかし試験のQuadが不正です。");

    var inputSize = new Vector2(1000, 500);
    var fivePixelsInside = mapping.LocalToInput.Transform(new Vector2(0.00625f, 0.5f));
    NearlyEqual(
        0.5f,
        QuadFeatherReference(
            mapping.InputToLocal,
            fivePixelsInside,
            inputSize,
            10),
        "Quad境界ぼかしを入力ピクセル距離へ換算できていません。",
        0.0001f);
    NearlyEqual(
        1,
        QuadFeatherReference(
            mapping.InputToLocal,
            mapping.LocalToInput.Transform(new Vector2(0.5f, 0.5f)),
            inputSize,
            10),
        "Quad中央の境界ぼかしは1である必要があります。");
    NearlyEqual(
        0,
        QuadFeatherReference(
            mapping.InputToLocal,
            mapping.LocalToInput.Transform(new Vector2(-0.01f, 0.5f)),
            inputSize,
            10),
        "Quad外側の境界ぼかしは0である必要があります。");
}
static void VerifyRegionRotationRoundTrip()
{
    var centerPixels = new Vector2(960, 540);
    var sizePixels = new Vector2(800, 400);
    const float rotationDegrees = 35;
    var rotationRadians = rotationDegrees * MathF.PI / 180;
    var rotationCos = MathF.Cos(rotationRadians);
    var rotationSin = MathF.Sin(rotationRadians);
    var regionPositions = new[]
    {
        Vector2.Zero,
        new Vector2(0.25f, 0.75f),
        new Vector2(0.5f, 0.5f),
        Vector2.One,
    };

    foreach (var expected in regionPositions)
    {
        var inputPixelPosition = RegionLocalToInput(
            expected,
            centerPixels,
            sizePixels,
            rotationCos,
            rotationSin);
        var actual = InputToRegionLocal(
            inputPixelPosition,
            centerPixels,
            sizePixels,
            rotationCos,
            rotationSin);

        Vector2Equal(
            expected,
            actual,
            $"回転領域の往復変換が一致しません。RegionPosition={expected}",
            0.00001f);
    }
}

static void VerifyRegionShapeMask()
{
    var halfSizePixels = new Vector2(400, 200);

    NearlyEqual(
        1,
        RegionMaskReference(
            GlassWipeRegionShape.Rectangle,
            Vector2.Zero,
            halfSizePixels,
            0),
        "四角形の中心は領域内である必要があります。");
    NearlyEqual(
        0,
        RegionMaskReference(
            GlassWipeRegionShape.Rectangle,
            new Vector2(401, 0),
            halfSizePixels,
            0),
        "四角形の外側は領域外である必要があります。");
    NearlyEqual(
        1,
        RegionMaskReference(
            GlassWipeRegionShape.Ellipse,
            Vector2.Zero,
            halfSizePixels,
            0),
        "楕円の中心は領域内である必要があります。");
    NearlyEqual(
        0,
        RegionMaskReference(
            GlassWipeRegionShape.Ellipse,
            halfSizePixels * 0.8f,
            halfSizePixels,
            0),
        "楕円の外側は領域外である必要があります。");

    var featheredEllipse = RegionMaskReference(
        GlassWipeRegionShape.Ellipse,
        new Vector2(395, 0),
        halfSizePixels,
        10);
    True(
        featheredEllipse is > 0.49f and < 0.52f,
        $"楕円の境界ぼかしがピクセル距離として評価されていません。Actual={featheredEllipse}");
    NearlyEqual(
        1,
        RegionMaskReference(
            GlassWipeRegionShape.FullScreen,
            new Vector2(10000, -10000),
            halfSizePixels,
            0),
        "全画面はローカル座標に関係なく領域内である必要があります。");
}

static void VerifyDebugViewEnum()
{
    var expected = new[]
    {
        (GlassWipeDebugView.Final, 0),
        (GlassWipeDebugView.RegionMask, 1),
        (GlassWipeDebugView.WipeMask, 2),
        (GlassWipeDebugView.Blurred, 3),
        (GlassWipeDebugView.RegionLocalUv, 4),
        (GlassWipeDebugView.QuadValidity, 5),
#if DEBUG
        (GlassWipeDebugView.OutsideDropletCoverage, 6),
        (GlassWipeDebugView.CoordinatePhase, 7),
        (GlassWipeDebugView.RegionCoordinateDelta, 8),
        (GlassWipeDebugView.OutsideDropletCellHash, 9),
        (GlassWipeDebugView.OutsideDropletSeedBits, 10),
        (GlassWipeDebugView.OutsideDropletCellHashWithoutSine, 11),
        (GlassWipeDebugView.OutsideDropletCellCoordinateBits, 12),
        (GlassWipeDebugView.OutsideDropletHashInputBits, 13),
        (GlassWipeDebugView.OutsideDropletFinalHashBitsWithoutSine, 14),
        (GlassWipeDebugView.FixedReturnColor, 15),
        (GlassWipeDebugView.OutsideDropletMixedHashBitsWithoutSine, 16),
        (GlassWipeDebugView.OutsideDropletPreAvalancheHashBitsWithoutSine, 17),
        (GlassWipeDebugView.OutsideDropletWeightedHashBitsWithoutSine, 18),
        (GlassWipeDebugView.OutsideDropletAccumulatedHashBitsWithoutSine, 19),
        (GlassWipeDebugView.OutsideDropletMediumOnlyHashInputGreenBitWithoutSine, 20),
        (GlassWipeDebugView.OutsideDropletMediumOnlyCellXGreenBitWithoutSine, 21),
        (GlassWipeDebugView.OutsideDropletMediumOnlyInlineHashInputGreenBitWithoutSine, 22),
        (GlassWipeDebugView.OutsideDropletMediumOnlyCellYGreenBitWithoutSine, 23),
#endif
    };

    Equal(expected.Length, Enum.GetValues<GlassWipeDebugView>().Length, "Debug Viewの要素数が不正です。");
    foreach (var (view, value) in expected)
    {
        Equal(value, (int)view, $"{view}の値が不正です。");
    }

    Equal(
        GlassWipeDebugView.Final,
        GlassWipeParameterSanitizer.SanitizeDebugView((GlassWipeDebugView)99),
        "未定義値はFinalへ補正する必要があります。");
}

static void VerifyStrokeSplit()
{
    var geometry = WipeMaskGeometry.Create(1000, 1000);
    var samples = new[]
    {
        Sample(0, 0.1f, 0.5f, 1),
        Sample(1, 0.2f, 0.5f, 1),
        Sample(2, 0.4f, 0.5f, 0),
        Sample(3, 0.6f, 0.5f, 0),
        Sample(4, 0.8f, 0.5f, 1),
        Sample(5, 0.9f, 0.5f, 1),
    };

    var stamps = WipeBrushStampGenerator.Generate(samples, 0, geometry);
    True(stamps.Count > 2, "有効な2区間からスタンプが生成されていません。");
    True(
        stamps.Any(stamp => stamp.Center.X < WipeMaskRenderer.MaskSize * 0.25f),
        "最初のStrokeが生成されていません。");
    True(
        stamps.Any(stamp => stamp.Center.X > WipeMaskRenderer.MaskSize * 0.75f),
        "2本目のStrokeが生成されていません。");
    True(
        stamps.All(stamp =>
            stamp.Center.X <= WipeMaskRenderer.MaskSize * 0.25f ||
            stamp.Center.X >= WipeMaskRenderer.MaskSize * 0.75f),
        "接触率0の区間をまたぐ不要なスタンプが生成されました。");

    var noContact = WipeBrushStampGenerator.Generate(
        [Sample(0, 0.5f, 0.5f, 0)],
        0,
        geometry);
    Equal(0, noContact.Count, "接触率0のサンプルは描画してはいけません。");
}

static void VerifySpatialResampling()
{
    const float size = 0.1f;
    const float regionWidth = 1600;
    const float regionHeight = 800;
    var geometry = WipeMaskGeometry.Create(regionWidth, regionHeight);
    var samples = new[]
    {
        Sample(0, 0.05f, 0.5f, 1, size),
        Sample(1, 0.95f, 0.5f, 1, size),
    };

    var stamps = WipeBrushStampGenerator.Generate(samples, 0, geometry);
    True(stamps.Count > 2, "長い線分へ中間スタンプが追加されていません。");

    var maximumSpacing = size * WipeBrushStampGenerator.SpacingRatio;
    for (var index = 1; index < stamps.Count; index++)
    {
        var previous = stamps[index - 1].Center / WipeMaskRenderer.MaskSize;
        var current = stamps[index].Center / WipeMaskRenderer.MaskSize;
        var actualSpacing = geometry.MeasureInShortSideUnits(previous, current);
        True(
            actualSpacing <= maximumSpacing + 0.000001f,
            $"スタンプ間隔が上限を超えました。Actual={actualSpacing}, Max={maximumSpacing}");
    }

    var first = stamps[0];
    var radiusInInputX = first.RadiusX / WipeMaskRenderer.MaskSize * regionWidth;
    var radiusInInputY = first.RadiusY / WipeMaskRenderer.MaskSize * regionHeight;
    NearlyEqual(
        radiusInInputX,
        radiusInInputY,
        "横長Regionでも入力ピクセル上の円ブラシ半径を一致させる必要があります。");
}

static void VerifyEllipseBrushGeometry()
{
    const float size = 0.2f;
    const float aspectRatio = 2;
    var rotationRadians = MathF.PI / 4;
    var geometry = WipeMaskGeometry.Create(1600, 800);
    var defaultStamp = WipeBrushStampGenerator.Generate(
        [Sample(0, 0.5f, 0.5f, 1, size)],
        0,
        geometry).Single();

    Equal(
        Matrix3x2.Identity,
        defaultStamp.Transform,
        "縦横比100%・回転0°では従来の円ブラシ変換を維持する必要があります。");
    NearlyEqual(
        defaultStamp.RadiusX / geometry.ScaleX,
        defaultStamp.RadiusY / geometry.ScaleY,
        "初期値のブラシは入力ピクセル上で円になる必要があります。");

    var stamp = WipeBrushStampGenerator.Generate(
        [
            Sample(
                0,
                0.5f,
                0.5f,
                1,
                size,
                aspectRatio: aspectRatio,
                rotationRadians: rotationRadians),
        ],
        0,
        geometry).Single();
    var transformedCenter = Vector2.Transform(stamp.Center, stamp.Transform);
    var majorEndpoint = Vector2.Transform(
        stamp.Center + new Vector2(stamp.RadiusX, 0),
        stamp.Transform);
    var minorEndpoint = Vector2.Transform(
        stamp.Center + new Vector2(0, stamp.RadiusY),
        stamp.Transform);
    var majorVector = ToShortSideVector(
        majorEndpoint - transformedCenter,
        geometry);
    var minorVector = ToShortSideVector(
        minorEndpoint - transformedCenter,
        geometry);
    var rotationCos = MathF.Cos(rotationRadians);
    var rotationSin = MathF.Sin(rotationRadians);

    Vector2Equal(
        stamp.Center,
        transformedCenter,
        "回転変換でブラシ中心が移動してはいけません。",
        0.0001f);
    Vector2Equal(
        new Vector2(rotationCos, rotationSin) *
            (size * aspectRatio * 0.5f),
        majorVector,
        "楕円の長径と回転角が入力ピクセル上で一致しません。",
        0.00001f);
    Vector2Equal(
        new Vector2(-rotationSin, rotationCos) * (size * 0.5f),
        minorVector,
        "楕円の短径と回転角が入力ピクセル上で一致しません。",
        0.00001f);

    var sanitizedStamp = WipeBrushStampGenerator.Generate(
        [
            Sample(
                0,
                0.5f,
                0.5f,
                1,
                size,
                aspectRatio: float.NaN,
                rotationRadians: float.NaN),
        ],
        0,
        geometry).Single();
    Equal(
        Matrix3x2.Identity,
        sanitizedStamp.Transform,
        "非有限の回転角は0°へ補正する必要があります。");
    NearlyEqual(
        defaultStamp.RadiusX,
        sanitizedStamp.RadiusX,
        "非有限の縦横比は100%へ補正する必要があります。");

    var narrowStamp = WipeBrushStampGenerator.Generate(
        [
            Sample(
                0,
                0.5f,
                0.5f,
                1,
                size,
                aspectRatio: 0.25f),
        ],
        0,
        geometry).Single();
    NearlyEqual(
        defaultStamp.RadiusX * 0.25f,
        narrowStamp.RadiusX,
        "25%の縦横比倍率を横半径へ反映する必要があります。",
        0.0001f);
}

static void VerifyBrushShapes()
{
    Equal(0, (int)GlassWipeBrushShape.Circle, "円の値が不正です。");
    Equal("円", GetDisplayName(GlassWipeBrushShape.Circle), "円ブラシの表示名が不正です。");
    Equal(1, (int)GlassWipeBrushShape.Rectangle, "四角形の値が不正です。");
    Equal(2, (int)GlassWipeBrushShape.Hand, "手形の値が不正です。");
    Equal(3, (int)GlassWipeBrushShape.ShoePrint, "靴跡の値が不正です。");
    Equal(4, (int)GlassWipeBrushShape.UserImage, "ユーザー画像の値が不正です。");
    Equal(5, (int)GlassWipeBrushShape.Ellipse, "楕円の値が不正です。");
    Equal("楕円", GetDisplayName(GlassWipeBrushShape.Ellipse), "楕円ブラシの表示名が不正です。");
    Equal(6, Enum.GetValues<GlassWipeBrushShape>().Length, "ブラシ形状数が不正です。");
    True(
        HasPropertyCustomAttribute(
            typeof(GlassWipeBrushShape).Assembly.Location,
            "YMM4GlassWipe",
            nameof(GlassWipeVideoEffect),
            nameof(GlassWipeVideoEffect.BrushMirror),
            "YukkuriMovieMaker.Controls",
            "ToggleSliderAttribute"),
        "左右反転にはYMM4のトグルエディターが必要です。");
    Equal(
        GlassWipeBrushShape.Circle,
        GlassWipeParameterSanitizer.SanitizeBrushShape(
            (GlassWipeBrushShape)99),
        "不正なブラシ形状は円へ補正する必要があります。");
    True(
        !WipeMaskRenderer.RequiresAliasedOpacityMask(
            GlassWipeBrushShape.Circle) &&
        !WipeMaskRenderer.RequiresAliasedOpacityMask(
            GlassWipeBrushShape.Ellipse),
        "円と楕円はPerPrimitive描画を使用する必要があります。");
    foreach (var shape in new[]
             {
                 GlassWipeBrushShape.Rectangle,
                 GlassWipeBrushShape.Hand,
                 GlassWipeBrushShape.ShoePrint,
                 GlassWipeBrushShape.UserImage,
             })
    {
        True(
            WipeMaskRenderer.RequiresAliasedOpacityMask(shape),
            $"{shape} のFillOpacityMaskにはAliased描画が必要です。");
    }

    var masks = new Dictionary<GlassWipeBrushShape, WipeBrushBinaryMask>();
    foreach (var shape in new[]
             {
                 GlassWipeBrushShape.Rectangle,
                 GlassWipeBrushShape.Hand,
                 GlassWipeBrushShape.ShoePrint,
             })
    {
        var mask = WipeBrushMaskRasterizer.CreateBinaryMask(shape);
        masks.Add(shape, mask);
        Equal(WipeBrushMaskRasterizer.MaskSize, mask.Size, $"{shape} のマスク寸法");
        Equal(mask.Size * mask.Size, mask.Pixels.Length, $"{shape} の画素数");
        var filledPixels = mask.Pixels.Count(pixel => pixel);
        if (shape == GlassWipeBrushShape.Rectangle)
        {
            Equal(mask.Pixels.Length, filledPixels, "四角形は指定寸法の全域を使用する必要があります。");
        }
        else
        {
            True(
                filledPixels > mask.Pixels.Length / 20 &&
                filledPixels < mask.Pixels.Length * 19 / 20,
                $"{shape} の有効画素面積が不正です。");
        }

        var hardAlpha = WipeBrushMaskRasterizer.CreateFeatheredAlpha(mask, 0);
        True(
            hardAlpha.All(alpha => alpha is 0 or byte.MaxValue),
            $"{shape} の柔らかさ0は二値alphaである必要があります。");
        var softAlpha = WipeBrushMaskRasterizer.CreateFeatheredAlpha(mask, 0.15f);
        True(
            softAlpha.Any(alpha => alpha is > 0 and < byte.MaxValue),
            $"{shape} の形状内側にぼかし階調が必要です。");
        True(
            softAlpha.Zip(mask.Pixels).All(pair => pair.Second || pair.First == 0),
            $"{shape} のぼかしが形状外へ漏れてはいけません。");
    }

    var rectangleAlpha = WipeBrushMaskRasterizer.CreateFeatheredAlpha(
        masks[GlassWipeBrushShape.Rectangle],
        0.15f);
    var rectangleCorner = WipeBrushMaskRasterizer.Padding *
        WipeBrushMaskRasterizer.MaskSize + WipeBrushMaskRasterizer.Padding;
    True(
        rectangleAlpha[rectangleCorner] > 0,
        "四角形の角を円形ぼかしで消してはいけません。");
    True(
        !masks[GlassWipeBrushShape.Hand].Pixels.SequenceEqual(
            masks[GlassWipeBrushShape.ShoePrint].Pixels),
        "手形と靴跡は異なるPNG形状である必要があります。");
    var rectangleBounds = GetMaskBounds(
        masks[GlassWipeBrushShape.Rectangle]);
    var handBounds = GetMaskBounds(masks[GlassWipeBrushShape.Hand]);
    var shoePrintBounds = GetMaskBounds(
        masks[GlassWipeBrushShape.ShoePrint]);
    NearlyEqual(
        1,
        rectangleBounds.Width / (float)rectangleBounds.Height,
        "四角形マスクの縦横比",
        0.01f);
    var handAspectRatio = handBounds.Width / (float)handBounds.Height;
    True(
        handAspectRatio is > 0.75f and < 0.9f,
        "手形PNGは手首なしの直立形状である必要があります。");
    True(
        GetLowerPalmWidthRatio(masks[GlassWipeBrushShape.Hand]) > 0.4f,
        "手形PNGの下部に細長い手首や腕を含めてはいけません。");
    NearlyEqual(
        1189f / 430,
        shoePrintBounds.Width / (float)shoePrintBounds.Height,
        "靴跡PNGの縦横比",
        0.05f);

    var samples = new[] { Sample(0, 0.5f, 0.5f, 1, 0.2f, aspectRatio: 2) };
    var geometry = WipeMaskGeometry.Create(1000, 1000);
    foreach (var shape in Enum.GetValues<GlassWipeBrushShape>())
    {
        var stamps = WipeBrushStampGenerator.Generate(
            samples,
            0,
            geometry,
            new WipePathStyle(0, 0, 1, BrushShape: shape));
        Equal(1, stamps.Count, $"{shape} の単一点スタンプ数");
        Equal(shape, stamps[0].Shape, $"{shape} がスタンプへ渡されていません。");
        var expectedRadiusX = shape == GlassWipeBrushShape.ShoePrint ? 102.4f : 204.8f;
        var expectedRadiusY = shape == GlassWipeBrushShape.ShoePrint ? 204.8f : 102.4f;
        NearlyEqual(expectedRadiusX, stamps[0].RadiusX, $"{shape} の回転前X半径", 0.001f);
        NearlyEqual(expectedRadiusY, stamps[0].RadiusY, $"{shape} の回転前Y半径", 0.001f);
    }
}

static void VerifyUserBrushLibrary()
{
    var temporaryDirectory = Path.Combine(
        FindRepositoryRoot(),
        "tmp",
        "verification",
        $"user-brush-{Guid.NewGuid():N}");
    Directory.CreateDirectory(temporaryDirectory);
    try
    {
        var alphaPath = Path.Combine(temporaryDirectory, "alpha.png");
        var opaquePath = Path.Combine(temporaryDirectory, "opaque.png");
        var rgbPath = Path.Combine(temporaryDirectory, "rgb.png");
        var alphaBytes = CreateTestPng(4, 3, includeAlpha: true, allOpaque: false);
        var opaqueBytes = CreateTestPng(4, 3, includeAlpha: true, allOpaque: true);
        var rgbBytes = CreateTestPng(4, 3, includeAlpha: false, allOpaque: true);
        File.WriteAllBytes(alphaPath, alphaBytes);
        File.WriteAllBytes(opaquePath, opaqueBytes);
        File.WriteAllBytes(rgbPath, rgbBytes);

        True(
            UserBrushPngValidator.TryValidate(
                alphaBytes,
                "alpha.png",
                out var alphaMask,
                out var alphaHash,
                out var alphaError),
            alphaError ?? "alpha付きPNGを検証できませんでした。");
        Equal(64, alphaHash.Length, "ユーザーブラシのSHA-256文字数");
        Equal(
            WipeBrushMaskRasterizer.MaskSize * WipeBrushMaskRasterizer.MaskSize,
            alphaMask.Pixels.Length,
            "ユーザーブラシの正規化画素数");
        var alphaBounds = GetMaskBounds(alphaMask);
        True(
            alphaBounds.Width is > 100 and < 180 &&
            alphaBounds.Height == WipeBrushMaskRasterizer.MaskSize,
            "透明余白を切り落とさず、元PNGキャンバス内の位置を保持する必要があります。");
        True(
            UserBrushPngValidator.TryValidate(
                opaqueBytes,
                "opaque.png",
                out var opaqueMask,
                out _,
                out var opaqueError),
            opaqueError ?? "全不透明PNGを検証できませんでした。");
        var opaqueBounds = GetMaskBounds(opaqueMask);
        Equal(4, opaqueMask.SourcePixelWidth, "全不透明PNGの元キャンバス幅");
        Equal(3, opaqueMask.SourcePixelHeight, "全不透明PNGの元キャンバス高さ");
        NearlyEqual(
            1,
            opaqueBounds.Width / (float)opaqueBounds.Height,
            "内部マスクは元キャンバス全体を正方形へ保持する必要があります。",
            0.02f);
        NearlyEqual(
            4f / 3,
            opaqueMask.SourcePixelWidth / (float)opaqueMask.SourcePixelHeight,
            "描画寸法には元PNGキャンバス比率を使用する必要があります。",
            0.001f);
        True(
            !UserBrushPngValidator.TryValidate(
                rgbBytes,
                "rgb.png",
                out _,
                out _,
                out var rgbError) &&
            rgbError?.Contains("alpha", StringComparison.OrdinalIgnoreCase) == true,
            "alphaチャネルのないPNGを拒否する必要があります。");

        var oversizedHeader = alphaBytes.ToArray();
        System.Buffers.Binary.BinaryPrimitives.WriteUInt32BigEndian(
            oversizedHeader.AsSpan(16, 4),
            UserBrushLibrary.MaximumDimension + 1u);
        True(
            !UserBrushPngValidator.TryValidate(
                oversizedHeader,
                "oversized.png",
                out _,
                out _,
                out var dimensionError) &&
            dimensionError?.Contains("1920", StringComparison.Ordinal) == true,
            "1920pxを超えるPNGをデコード前に拒否する必要があります。");
        True(
            !UserBrushPngValidator.TryValidate(
                new byte[UserBrushLibrary.MaximumFileBytes + 1],
                "large.png",
                out _,
                out _,
                out _),
            "8 MiBを超える入力を拒否する必要があります。");

        var libraryRoot = Path.Combine(temporaryDirectory, "library");
        var library = new UserBrushLibrary(libraryRoot);
        Equal(0, library.Load(out var missingError).Count, "未作成ライブラリは空として扱う必要があります。");
        True(missingError is null, "未作成ライブラリはエラーにしてはいけません。");
        var oversizedSourcePath = Path.Combine(temporaryDirectory, "oversized-source.png");
        using (var oversizedSource = new FileStream(
                   oversizedSourcePath,
                   FileMode.CreateNew,
                   FileAccess.Write,
                   FileShare.None))
        {
            oversizedSource.SetLength(UserBrushLibrary.MaximumFileBytes + 1L);
        }

        True(
            !library.Register(
                "過大入力",
                oversizedSourcePath,
                out _,
                out _,
                out var oversizedSourceError) &&
            oversizedSourceError?.Contains("8 MiB", StringComparison.Ordinal) == true,
            "8 MiBを1 byte超える登録元PNGを拒否する必要があります。");
        Equal(0, library.Load(out _).Count, "過大PNG拒否後に一覧を変更してはいけません。");
        True(
            !UserBrushLibrary.TryValidateName("手形", out _, out var reservedError) &&
            reservedError?.Contains("内蔵", StringComparison.Ordinal) == true,
            "手形を予約名として拒否する必要があります。");
        True(
            !UserBrushLibrary.TryValidateName("靴跡", out _, out _),
            "靴跡を予約名として拒否する必要があります。");

        True(
            library.Register(
                "  ブラシA  ",
                alphaPath,
                out var first,
                out var firstReplaced,
                out var firstError),
            firstError ?? "ユーザーブラシを登録できませんでした。");
        True(!firstReplaced, "初回登録を置換として扱ってはいけません。");
        Equal("ブラシA", first.Name, "登録名の前後空白を除去する必要があります。");
        Equal(4, first.PixelWidth, "登録したPNGの元キャンバス幅");
        Equal(3, first.PixelHeight, "登録したPNGの元キャンバス高さ");
        True(first.Id != Guid.Empty, "固定ブラシIDを発行する必要があります。");
        True(File.Exists(library.GetImagePath(first)), "検証済みPNGをライブラリへコピーする必要があります。");
        var registryText = File.ReadAllText(library.RegistryPath);
        using (var registryJson = JsonDocument.Parse(registryText))
        {
            Equal(
                UserBrushLibraryFile.CurrentVersion,
                registryJson.RootElement.GetProperty("dataSchemaVersion").GetInt32(),
                "ユーザーブラシ一覧のschema version");
        }
        True(
            !registryText.Contains(alphaPath, StringComparison.OrdinalIgnoreCase),
            "登録元ファイルのパスを保存してはいけません。");
        True(
            library.TryLoadMask(first.Id, out _, out var loadedFirst, out var loadError),
            loadError ?? "登録したユーザーブラシを読み込めませんでした。");
        Equal(first.Id, loadedFirst.Id, "読み込み時のブラシID");

        True(
            library.Register(
                "ブラシA",
                opaquePath,
                out var replaced,
                out var wasReplaced,
                out var replaceError),
            replaceError ?? "同名ユーザーブラシを置き換えられませんでした。");
        True(wasReplaced, "同名登録を置換として報告する必要があります。");
        Equal(first.Id, replaced.Id, "同名置換で固定ブラシIDを維持する必要があります。");
        True(replaced.Revision > first.Revision, "同名置換で更新世代を進める必要があります。");

        True(
            library.Replace(
                replaced.Id,
                alphaPath,
                out var explicitlyReplaced,
                out var explicitError),
            explicitError ?? "選択ブラシを置き換えられませんでした。");
        True(
            explicitlyReplaced.Revision > replaced.Revision,
            "明示置換で更新世代を進める必要があります。");

        var atomicEntry = explicitlyReplaced;
        Directory.CreateDirectory(library.RegistryPath + ".tmp");
        True(
            !library.Replace(
                atomicEntry.Id,
                opaquePath,
                out _,
                out var atomicError) &&
            atomicError is not null,
            "一覧保存に失敗した置換を成功扱いしてはいけません。");
        Directory.Delete(library.RegistryPath + ".tmp");
        True(
            library.TryLoadMask(atomicEntry.Id, out _, out var afterFailure, out var afterFailureError),
            afterFailureError ?? "失敗した置換後に既存ブラシを維持できませんでした。");
        Equal(atomicEntry.Revision, afterFailure.Revision, "置換失敗時は旧世代を維持する必要があります。");

        True(
            library.Register(
                "欠落確認",
                alphaPath,
                out var missing,
                out _,
                out var missingRegisterError),
            missingRegisterError ?? "欠落確認ブラシを登録できませんでした。");
        File.Delete(library.GetImagePath(missing));
        True(
            !library.TryLoadMask(missing.Id, out _, out _, out var missingImageError) &&
            missingImageError?.Contains("再登録", StringComparison.Ordinal) == true,
            "欠落画像で再登録方法を案内する必要があります。");

        True(
            library.Register(
                "破損確認",
                alphaPath,
                out var corrupt,
                out _,
                out var corruptRegisterError),
            corruptRegisterError ?? "破損確認ブラシを登録できませんでした。");
        File.WriteAllBytes(library.GetImagePath(corrupt), [1, 2, 3]);
        True(
            !library.TryLoadMask(corrupt.Id, out _, out _, out var corruptError) &&
            corruptError?.Contains("再登録", StringComparison.Ordinal) == true,
            "破損画像で再登録方法を案内する必要があります。");

        True(
            library.Register(
                "過大管理画像",
                alphaPath,
                out var oversizedManaged,
                out _,
                out var oversizedManagedRegisterError),
            oversizedManagedRegisterError ?? "過大管理画像試験用ブラシを登録できませんでした。");
        using (var oversizedManagedFile = new FileStream(
                   library.GetImagePath(oversizedManaged),
                   FileMode.Open,
                   FileAccess.Write,
                   FileShare.None))
        {
            oversizedManagedFile.SetLength(UserBrushLibrary.MaximumFileBytes + 1L);
        }

        True(
            !library.TryLoadMask(
                oversizedManaged.Id,
                out _,
                out _,
                out var oversizedManagedError) &&
            oversizedManagedError?.Contains("再登録", StringComparison.Ordinal) == true,
            "8 MiBを超えた管理画像で再登録方法を案内する必要があります。");

        True(
            library.Delete(atomicEntry.Id, out var deleteError),
            deleteError ?? "ユーザーブラシを削除できませんでした。");
        True(
            library.Load(out _).All(entry => entry.Id != atomicEntry.Id) &&
            !File.Exists(library.GetImagePath(atomicEntry)),
            "削除時は一覧とPNGの両方から完全削除する必要があります。");

        var limitLibrary = new UserBrushLibrary(
            Path.Combine(temporaryDirectory, "limit-library"));
        for (var index = 0; index < UserBrushLibrary.MaximumBrushCount; index++)
        {
            True(
                limitLibrary.Register(
                    $"制限{index}",
                    alphaPath,
                    out _,
                    out _,
                    out var limitError),
                limitError ?? $"{index + 1}件目を登録できませんでした。");
        }

        True(
            !limitLibrary.Register(
                "上限超過",
                alphaPath,
                out _,
                out _,
                out var overLimitError) &&
            overLimitError?.Contains("100", StringComparison.Ordinal) == true,
            "100件を超えるユーザーブラシを拒否する必要があります。");

        var oversizedRegistryRoot = Path.Combine(temporaryDirectory, "oversized-registry");
        Directory.CreateDirectory(oversizedRegistryRoot);
        var oversizedRegistryLibrary = new UserBrushLibrary(oversizedRegistryRoot);
        using (var oversizedRegistry = new FileStream(
                   oversizedRegistryLibrary.RegistryPath,
                   FileMode.CreateNew,
                   FileAccess.Write,
                   FileShare.None))
        {
            oversizedRegistry.SetLength(UserBrushLibrary.MaximumRegistryFileBytes + 1L);
        }

        var oversizedRegistryLength = new FileInfo(
            oversizedRegistryLibrary.RegistryPath).Length;
        Equal(
            0,
            oversizedRegistryLibrary.Load(out var oversizedRegistryError).Count,
            "上限超過のユーザーブラシ一覧は空として扱う必要があります。");
        True(
            oversizedRegistryError?.Contains("256 KiB", StringComparison.Ordinal) == true,
            "一覧の容量超過理由を表示する必要があります。");
        True(
            !oversizedRegistryLibrary.Register(
                "上書き禁止",
                alphaPath,
                out _,
                out _,
                out _),
            "上限超過の一覧を暗黙に上書きしてはいけません。");
        Equal(
            oversizedRegistryLength,
            new FileInfo(oversizedRegistryLibrary.RegistryPath).Length,
            "上限超過の一覧ファイルを保持する必要があります。");

        var legacyRegistryRoot = Path.Combine(temporaryDirectory, "legacy-registry");
        Directory.CreateDirectory(legacyRegistryRoot);
        var legacyRegistryLibrary = new UserBrushLibrary(legacyRegistryRoot);
        var legacyRegistryText = JsonSerializer.Serialize(new
        {
            dataSchemaVersion = 1,
            brushes = new[]
            {
                new
                {
                    id = Guid.NewGuid(),
                    name = "旧ブラシ",
                    fileName = "legacy.png",
                    contentHash = new string('0', 64),
                    revision = 1,
                },
            },
        });
        File.WriteAllText(legacyRegistryLibrary.RegistryPath, legacyRegistryText);
        var legacyRegistryBytes = File.ReadAllBytes(legacyRegistryLibrary.RegistryPath);
        Equal(
            0,
            legacyRegistryLibrary.Load(out var legacyRegistryError).Count,
            "旧schemaのユーザーブラシ一覧を移行してはいけません。");
        True(
            legacyRegistryError?.Contains("互換性", StringComparison.Ordinal) == true &&
            legacyRegistryError?.Contains("再登録", StringComparison.Ordinal) == true,
            "旧ユーザーブラシ一覧には再登録方法を表示する必要があります。");
        True(
            !legacyRegistryLibrary.Register(
                "上書き禁止",
                alphaPath,
                out _,
                out _,
                out _),
            "旧schemaの一覧を暗黙に上書きしてはいけません。");
        True(
            File.ReadAllBytes(legacyRegistryLibrary.RegistryPath)
                .SequenceEqual(legacyRegistryBytes),
            "旧schemaのユーザーブラシ一覧を変更してはいけません。");

        var unknownRegistryRoot = Path.Combine(temporaryDirectory, "unknown-registry");
        Directory.CreateDirectory(unknownRegistryRoot);
        var unknownRegistryLibrary = new UserBrushLibrary(unknownRegistryRoot);
        File.WriteAllText(
            unknownRegistryLibrary.RegistryPath,
            JsonSerializer.Serialize(new
            {
                dataSchemaVersion = UserBrushLibraryFile.CurrentVersion,
                brushes = Array.Empty<object>(),
                unknownProperty = true,
            }));
        var unknownRegistryBytes = File.ReadAllBytes(unknownRegistryLibrary.RegistryPath);
        Equal(
            0,
            unknownRegistryLibrary.Load(out var unknownRegistryError).Count,
            "現行schemaでも未知項目を含む一覧を受理してはいけません。");
        True(
            unknownRegistryError?.Contains("互換性", StringComparison.Ordinal) == true,
            "未知項目を含む一覧には互換性エラーを表示する必要があります。");
        True(
            !unknownRegistryLibrary.Register(
                "上書き禁止",
                alphaPath,
                out _,
                out _,
                out _),
            "未知項目を含む一覧を暗黙に上書きしてはいけません。");
        True(
            File.ReadAllBytes(unknownRegistryLibrary.RegistryPath)
                .SequenceEqual(unknownRegistryBytes),
            "未知項目を含むユーザーブラシ一覧を変更してはいけません。");

        var invalidRegistryRoot = Path.Combine(temporaryDirectory, "invalid-registry");
        Directory.CreateDirectory(invalidRegistryRoot);
        var invalidRegistryLibrary = new UserBrushLibrary(invalidRegistryRoot);
        File.WriteAllBytes(invalidRegistryLibrary.RegistryPath, [0xFF]);
        Equal(
            0,
            invalidRegistryLibrary.Load(out var invalidRegistryError).Count,
            "無効UTF-8の一覧は空として扱う必要があります。");
        True(invalidRegistryError is not null, "無効UTF-8の一覧理由を返す必要があります。");
        True(
            !invalidRegistryLibrary.Register(
                "上書き禁止",
                alphaPath,
                out _,
                out _,
                out _),
            "無効UTF-8の一覧を暗黙に上書きしてはいけません。");
        True(
            File.ReadAllBytes(invalidRegistryLibrary.RegistryPath).SequenceEqual(new byte[] { 0xFF }),
            "無効UTF-8の一覧ファイルを保持する必要があります。");
    }
    finally
    {
        if (Directory.Exists(temporaryDirectory))
        {
            Directory.Delete(temporaryDirectory, recursive: true);
        }
    }
}

static void VerifyBoundedFileReads()
{
    var temporaryDirectory = Path.Combine(
        FindRepositoryRoot(),
        "tmp",
        "verification",
        $"bounded-file-{Guid.NewGuid():N}");
    Directory.CreateDirectory(temporaryDirectory);
    try
    {
        const int maximumBytes = 4096;
        var exactPath = Path.Combine(temporaryDirectory, "exact.bin");
        var exactBytes = Enumerable.Range(0, maximumBytes)
            .Select(index => (byte)(index % 251))
            .ToArray();
        File.WriteAllBytes(exactPath, exactBytes);
        True(
            BoundedFileReader.TryReadBytes(
                exactPath,
                maximumBytes,
                out var loaded,
                out var exactExceeded,
                out var exactException),
            exactException?.Message ?? "上限と同じ容量のファイルを読み込めませんでした。");
        True(!exactExceeded, "上限と同じ容量を超過扱いにしてはいけません。");
        True(exactBytes.SequenceEqual(loaded), "上限付き読込で内容を変えてはいけません。");

        var oversizedPath = Path.Combine(temporaryDirectory, "oversized.bin");
        using (var oversized = new FileStream(
                   oversizedPath,
                   FileMode.CreateNew,
                   FileAccess.Write,
                   FileShare.None))
        {
            oversized.SetLength(maximumBytes + 1L);
        }

        True(
            !BoundedFileReader.TryReadBytes(
                oversizedPath,
                maximumBytes,
                out loaded,
                out var oversizedExceeded,
                out _) &&
            oversizedExceeded &&
            loaded.Length == 0,
            "上限を1 byte超えるファイルは配列確保前に拒否する必要があります。");

        var invalidUtf8Path = Path.Combine(temporaryDirectory, "invalid.json");
        File.WriteAllBytes(invalidUtf8Path, [0xFF]);
        True(
            !BoundedFileReader.TryDeserializeJson<JsonElement>(
                invalidUtf8Path,
                maximumBytes,
                new JsonSerializerOptions(),
                out _,
                out var invalidExceeded,
                out var invalidException) &&
            !invalidExceeded &&
            invalidException is JsonException,
            "無効UTF-8のJSONを破損として拒否する必要があります。");
    }
    finally
    {
        if (Directory.Exists(temporaryDirectory))
        {
            Directory.Delete(temporaryDirectory, recursive: true);
        }
    }
}

static void VerifyUserBrushExchange()
{
    var id = Guid.NewGuid();
    var snapshot = UserBrushExchangeCodec.EncodeSnapshot(id, 7, 320, 180);
    True(
        UserBrushExchangeCodec.TryDecode(snapshot, out var decodedSnapshot),
        "ユーザーブラシ選択スナップショットを復元できる必要があります。");
    Equal(id, decodedSnapshot.UserBrushId, "選択スナップショットのブラシID");
    Equal(7L, decodedSnapshot.Revision, "選択スナップショットの更新世代");
    Equal(320, decodedSnapshot.PixelWidth, "選択スナップショットの画像幅");
    Equal(180, decodedSnapshot.PixelHeight, "選択スナップショットの画像高さ");
    True(!decodedSnapshot.Apply, "取得用スナップショットを適用要求にしてはいけません。");

    var request = UserBrushExchangeCodec.EncodeApply(id, 8, 640, 360);
    True(
        UserBrushExchangeCodec.TryDecode(request, out var decodedRequest) &&
        decodedRequest.Apply &&
        decodedRequest.PixelWidth == 640 &&
        decodedRequest.PixelHeight == 360,
        "ユーザーブラシの適用要求を復元できる必要があります。");
    True(
        !UserBrushExchangeCodec.TryDecode("{}", out _),
        "必須版のないユーザーブラシ交換値を拒否する必要があります。");

    var preset = DetailedWipePresetFactory.Create(DetailedWipeBuiltInPreset.Heart);
    preset.BrushShape = GlassWipeBrushShape.UserImage;
    preset.UserBrushId = id;
    preset.UserBrushRevision = 8;
    preset.UserBrushPixelWidth = 640;
    preset.UserBrushPixelHeight = 360;
    var encodedPreset = DetailedWipePresetCodec.Encode(preset);
    True(
        DetailedWipePresetCodec.TryDecode(encodedPreset, out var decodedPreset),
        "ユーザー画像プリセットを復元できる必要があります。");
    Equal(GlassWipeBrushShape.UserImage, decodedPreset.BrushShape, "ユーザー画像のプリセット形状");
    Equal(id, decodedPreset.UserBrushId, "プリセットの固定ブラシID");
    Equal(8L, decodedPreset.UserBrushRevision, "プリセットの更新世代");
    Equal(640, decodedPreset.UserBrushPixelWidth, "プリセットの元PNG幅");
    Equal(360, decodedPreset.UserBrushPixelHeight, "プリセットの元PNG高さ");

    var selectedEffect = new GlassWipeVideoEffect
    {
        BrushShape = GlassWipeBrushShape.UserImage,
        UserBrushId = id,
        UserBrushRevision = 8,
        UserBrushPixelWidth = 640,
        UserBrushPixelHeight = 360,
    };
    var clonedEffect = YukkuriMovieMaker.Json.Json.GetClone(selectedEffect)
        ?? throw new InvalidOperationException("ユーザー画像を選択したEffectを複製できませんでした。");
    Equal(id, clonedEffect.UserBrushId, "プロジェクト保存往復のユーザーブラシID");
    Equal(8L, clonedEffect.UserBrushRevision, "プロジェクト保存往復の更新世代");
    Equal(640, clonedEffect.UserBrushPixelWidth, "プロジェクト保存往復の元PNG幅");
    Equal(360, clonedEffect.UserBrushPixelHeight, "プロジェクト保存往復の元PNG高さ");

    True(
        DetailedWipePresetStateMerger.TryMerge(
            DetailedWipePresetFactory.Create(DetailedWipeBuiltInPreset.Heart),
            decodedPreset,
            DetailedWipePresetScope.Brush,
            out var mergedBrush),
        "ユーザー画像のブラシプリセットを適用できる必要があります。");
    Equal(8L, mergedBrush.UserBrushRevision, "ブラシプリセット適用後の更新世代");
    Equal(640, mergedBrush.UserBrushPixelWidth, "ブラシプリセット適用後の元PNG幅");
    Equal(360, mergedBrush.UserBrushPixelHeight, "ブラシプリセット適用後の元PNG高さ");

    var builtIn = preset;
    builtIn.BrushShape = GlassWipeBrushShape.Hand;
    True(builtIn.TrySanitize(out var sanitizedBuiltIn), "内蔵ブラシプリセットを補正できる必要があります。");
    Equal(Guid.Empty, sanitizedBuiltIn.UserBrushId, "内蔵ブラシへユーザーブラシIDを残してはいけません。");

    var style = new WipePathStyle(
        0,
        0,
        1,
        BrushShape: GlassWipeBrushShape.UserImage,
        UserBrushId: id,
        UserBrushRevision: 8,
        UserBrushPixelWidth: 640,
        UserBrushPixelHeight: 360);
    var stamp = WipeBrushStampGenerator.Generate(
        [Sample(0, 0.5f, 0.5f, 1) with
        {
            BrushWidthPixels = 800,
            BrushHeightPixels = 450,
        }],
        0,
        WipeMaskGeometry.Create(1000, 1000),
        style).Single();
    Equal(id, stamp.UserBrushId, "固定ブラシIDをスタンプへ伝播する必要があります。");
    Equal(8L, stamp.UserBrushRevision, "更新世代をスタンプへ伝播する必要があります。");
    Equal(640, stamp.UserBrushPixelWidth, "元PNG幅をスタンプへ伝播する必要があります。");
    Equal(360, stamp.UserBrushPixelHeight, "元PNG高さをスタンプへ伝播する必要があります。");
    NearlyEqual(409.6f, stamp.RadiusX, "125%倍率後の描画半径X", 0.001f);
    NearlyEqual(230.4f, stamp.RadiusY, "125%倍率後の描画半径Y", 0.001f);

    var matchingEntry = new UserBrushEntry
    {
        Id = id,
        Revision = 8,
        PixelWidth = 640,
        PixelHeight = 360,
    };
    True(
        WipeMaskRenderer.MatchesUserBrushEntry(stamp, matchingEntry),
        "保存したユーザーブラシのID・更新世代・元PNG寸法が一致する場合だけ描画できる必要があります。");
    True(
        !WipeMaskRenderer.MatchesUserBrushEntry(
            stamp,
            new UserBrushEntry
            {
                Id = id,
                Revision = 9,
                PixelWidth = 640,
                PixelHeight = 360,
            }),
        "同一IDでも置換後の更新世代を旧設定へ流用してはいけません。");
    True(
        !WipeMaskRenderer.MatchesUserBrushEntry(
            stamp,
            new UserBrushEntry
            {
                Id = id,
                Revision = 8,
                PixelWidth = 320,
                PixelHeight = 360,
            }),
        "保存値と異なる元PNG寸法を旧設定へ流用してはいけません。");

    var otherId = Guid.NewGuid();
    var snapshotA = new WipePathSnapshot(
        0,
        0,
        60,
        [Sample(0, 0.5f, 0.5f, 1)],
        style);
    var snapshotB = new WipePathSnapshot(
        0,
        0,
        60,
        [Sample(0, 0.5f, 0.5f, 1)],
        style with { UserBrushId = otherId });
    var snapshotC = new WipePathSnapshot(
        0,
        0,
        60,
        [Sample(0, 0.5f, 0.5f, 1)],
        style with { UserBrushRevision = 9 });
    var snapshotD = new WipePathSnapshot(
        0,
        0,
        60,
        [Sample(0, 0.5f, 0.5f, 1)],
        style with { UserBrushPixelWidth = 320 });
    True(
        snapshotA.Fingerprint != snapshotB.Fingerprint &&
        snapshotA.Fingerprint != snapshotC.Fingerprint &&
        snapshotA.Fingerprint != snapshotD.Fingerprint,
        "ユーザーブラシID・更新世代・元PNG寸法をFingerprintへ含める必要があります。");

    True(
        HasPropertyCustomAttribute(
            typeof(GlassWipeBrushShape).Assembly.Location,
            "YMM4GlassWipe",
            nameof(GlassWipeVideoEffect),
            nameof(GlassWipeVideoEffect.UserBrushExchange),
            "YMM4GlassWipe",
            nameof(UserBrushEditorAttribute)),
        "ユーザーブラシ選択には専用プロパティエディターが必要です。");
    True(
        !HasPropertyCustomAttribute(
            typeof(GlassWipeBrushShape).Assembly.Location,
            "YMM4GlassWipe",
            nameof(GlassWipeVideoEffect),
            nameof(GlassWipeVideoEffect.UserBrushRevision),
            "System.Text.Json.Serialization",
            "JsonIgnoreAttribute"),
        "ユーザーブラシ更新世代はプロジェクトへ保存する必要があります。");
    Equal(
        0L,
        Convert.ToInt64(
            typeof(GlassWipeVideoEffect)
                .GetProperty(nameof(GlassWipeVideoEffect.UserBrushRevision))!
                .GetCustomAttribute<System.ComponentModel.DefaultValueAttribute>()!
                .Value),
        "ユーザーブラシ更新世代の欠落値");
    True(
        HasPropertyCustomAttribute(
            typeof(GlassWipeBrushShape).Assembly.Location,
            "YMM4GlassWipe",
            nameof(GlassWipeVideoEffect),
            nameof(GlassWipeVideoEffect.UserBrushRevision),
            "System.ComponentModel",
            "BrowsableAttribute"),
        "ユーザーブラシ更新世代を設定UIへ表示してはいけません。");
}

static byte[] CreateTestPng(
    int width,
    int height,
    bool includeAlpha,
    bool allOpaque)
{
    var format = includeAlpha ? PixelFormats.Bgra32 : PixelFormats.Bgr24;
    var bytesPerPixel = includeAlpha ? 4 : 3;
    var stride = width * bytesPerPixel;
    var pixels = new byte[stride * height];
    for (var y = 0; y < height; y++)
    {
        for (var x = 0; x < width; x++)
        {
            var index = y * stride + x * bytesPerPixel;
            pixels[index] = 32;
            pixels[index + 1] = 96;
            pixels[index + 2] = 192;
            if (includeAlpha)
            {
                pixels[index + 3] = allOpaque || x is > 0 and < 3
                    ? byte.MaxValue
                    : (byte)0;
            }
        }
    }

    var bitmap = BitmapSource.Create(
        width,
        height,
        96,
        96,
        format,
        null,
        pixels,
        stride);
    bitmap.Freeze();
    var encoder = new PngBitmapEncoder();
    encoder.Frames.Add(BitmapFrame.Create(bitmap));
    using var stream = new MemoryStream();
    encoder.Save(stream);
    return stream.ToArray();
}

static string FindRepositoryRoot()
{
    var directory = new DirectoryInfo(AppContext.BaseDirectory);
    while (directory is not null)
    {
        if (File.Exists(Path.Combine(directory.FullName, "YMM4GlassWipe.sln")))
        {
            return directory.FullName;
        }

        directory = directory.Parent;
    }

    throw new InvalidOperationException("検証用リポジトリのルートを特定できません。");
}

static void VerifyAsymmetricBrushMirrorAndRoundTrip()
{
    True(
        GlassWipeSimpleVisibility.IsBrushMirrorVisible(
            GlassWipeBrushShape.Hand),
        "手形では左右反転を表示する必要があります。");
    True(
        GlassWipeSimpleVisibility.IsBrushMirrorVisible(
            GlassWipeBrushShape.ShoePrint),
        "靴跡では左右反転を表示する必要があります。");
    True(
        GlassWipeSimpleVisibility.IsBrushMirrorVisible(
            GlassWipeBrushShape.UserImage),
        "ユーザー画像では左右反転を表示する必要があります。");
    True(
        !GlassWipeSimpleVisibility.IsBrushMirrorVisible(
            GlassWipeBrushShape.Circle) &&
        !GlassWipeSimpleVisibility.IsBrushMirrorVisible(
            GlassWipeBrushShape.Ellipse) &&
        !GlassWipeSimpleVisibility.IsBrushMirrorVisible(
            GlassWipeBrushShape.Rectangle),
        "円・楕円・四角形では左右反転を表示してはいけません。");

    var handTransform = WipeMaskRenderer.GetStampTransform(
        new WipeBrushStamp(
            Vector2.Zero,
            2,
            3,
            Matrix3x2.Identity,
            1,
            0,
            0,
            GlassWipeBrushShape.Hand,
            true));
    NearlyEqual(-2, handTransform.M11, "手形の左右反転はローカルXを反転する必要があります。");
    NearlyEqual(3, handTransform.M22, "手形の左右反転でローカルYを反転してはいけません。");

    var shoeTransform = WipeMaskRenderer.GetStampTransform(
        new WipeBrushStamp(
            Vector2.Zero,
            2,
            3,
            Matrix3x2.Identity,
            1,
            0,
            0,
            GlassWipeBrushShape.ShoePrint,
            true));
    NearlyEqual(2, shoeTransform.M11, "靴跡の左右反転で踵とつま先の方向を変えてはいけません。");
    NearlyEqual(-3, shoeTransform.M22, "靴跡の左右反転はローカルYを反転する必要があります。");

    var userTransform = WipeMaskRenderer.GetStampTransform(
        new WipeBrushStamp(
            Vector2.Zero,
            2,
            3,
            Matrix3x2.Identity,
            1,
            0,
            0,
            GlassWipeBrushShape.UserImage,
            true));
    NearlyEqual(-2, userTransform.M11, "ユーザー画像の左右反転はローカルXを反転する必要があります。");
    NearlyEqual(3, userTransform.M22, "ユーザー画像の左右反転でローカルYを反転してはいけません。");

    NearlyEqual(
        -MathF.PI / 2,
        WipeBrushStampGenerator.ResolveShapeBaseRotationRadians(
            GlassWipeBrushShape.ShoePrint),
        "靴跡は回転角0°でつま先を上へ向ける必要があります。");
    NearlyEqual(
        0,
        WipeBrushStampGenerator.ResolveShapeBaseRotationRadians(
            GlassWipeBrushShape.Hand),
        "手形の正立基準を靴跡の補正で変更してはいけません。");

    var rectangleTransform = WipeMaskRenderer.GetStampTransform(
        new WipeBrushStamp(
            Vector2.Zero,
            2,
            3,
            Matrix3x2.Identity,
            1,
            0,
            0,
            GlassWipeBrushShape.Rectangle,
            true));
    NearlyEqual(2, rectangleTransform.M11, "四角形へ左右反転を適用してはいけません。");
    NearlyEqual(3, rectangleTransform.M22, "四角形へ左右反転を適用してはいけません。");

    var geometry = WipeMaskGeometry.Create(1000, 1000);
    static Vector2 Axis(WipeBrushStamp stamp) =>
        Vector2.Normalize(new Vector2(stamp.Transform.M11, stamp.Transform.M12));
    var uprightShoe = WipeBrushStampGenerator.Generate(
        [Sample(0, 0.5f, 0.5f, 1)],
        0,
        geometry,
        new WipePathStyle(
            0,
            0,
            1,
            BrushShape: GlassWipeBrushShape.ShoePrint));
    True(
        Vector2.Dot(Axis(uprightShoe.Single()), new Vector2(0, -1)) > 0.9999f,
        "靴跡の回転角0°ではつま先を上へ向ける必要があります。");
    var manuallyRotatedShoe = WipeBrushStampGenerator.Generate(
        [Sample(0, 0.5f, 0.5f, 1, rotationRadians: MathF.PI / 2)],
        0,
        geometry,
        new WipePathStyle(
            0,
            0,
            1,
            BrushShape: GlassWipeBrushShape.ShoePrint));
    True(
        Vector2.Dot(Axis(manuallyRotatedShoe.Single()), Vector2.UnitX) > 0.9999f,
        "靴跡の手動回転角を上向き基準へ加算する必要があります。");
    foreach (var shape in new[]
             {
                 GlassWipeBrushShape.Hand,
                 GlassWipeBrushShape.ShoePrint,
                 GlassWipeBrushShape.UserImage,
             })
    {
        var style = new WipePathStyle(
            0,
            0,
            1,
            RotationFollow: 1,
            BrushShape: shape,
            BrushMirror: true);
        var outward = WipeBrushStampGenerator.Generate(
            [
                Sample(0, 0.2f, 0.5f, 1, accumulationGroup: 0),
                Sample(1, 0.8f, 0.5f, 1, accumulationGroup: 0),
            ],
            1,
            geometry,
            style);
        var returning = WipeBrushStampGenerator.Generate(
            [
                Sample(0, 0.8f, 0.5f, 1, accumulationGroup: 1),
                Sample(1, 0.2f, 0.5f, 1, accumulationGroup: 1),
            ],
            1,
            geometry,
            style);
        True(
            outward.All(stamp => stamp.Mirror) &&
            returning.All(stamp => stamp.Mirror),
            $"{shape} の左右反転を全スタンプへ保持する必要があります。");
        True(
            Vector2.Dot(Axis(outward[^1]), Axis(returning[^1])) > 0.9999f,
            $"{shape} は往復の折り返しで上下反転してはいけません。");
    }

    const int nonDivisibleItemLength = 487;
    const int firstReturnFrame = 122;
    foreach (var shape in new[]
             {
                 GlassWipeBrushShape.Hand,
                 GlassWipeBrushShape.ShoePrint,
                 GlassWipeBrushShape.UserImage,
             })
    {
        var firstReturn = WipeSimplePathGenerator.Generate(
            GlassWipeSimplePattern.OffsetRoundTrips,
            GlassWipePathInterpolation.CircularArc,
            new Vector2(15, 45),
            new Vector2(50, 20),
            new Vector2(85, 65),
            2,
            firstReturnFrame,
            nonDivisibleItemLength,
            13.5,
            70,
            geometry,
            10);
        var nextReturn = WipeSimplePathGenerator.Generate(
            GlassWipeSimplePattern.OffsetRoundTrips,
            GlassWipePathInterpolation.CircularArc,
            new Vector2(15, 45),
            new Vector2(50, 20),
            new Vector2(85, 65),
            2,
            firstReturnFrame + 1,
            nonDivisibleItemLength,
            13.5,
            70,
            geometry,
            10);
        var firstReturnStamps = WipeBrushStampGenerator.Generate(
            firstReturn.Samples,
            0,
            geometry,
            firstReturn.Style with
            {
                RotationFollow = 1,
                BrushShape = shape,
            });
        var nextReturnStamps = WipeBrushStampGenerator.Generate(
            nextReturn.Samples,
            0,
            geometry,
            nextReturn.Style with
            {
                RotationFollow = 1,
                BrushShape = shape,
            });
        var outwardAxis = Axis(firstReturnStamps.Last(stamp =>
            stamp.AccumulationGroup == 0));
        var firstReturnAxis = Axis(firstReturnStamps.Last(stamp =>
            stamp.AccumulationGroup == 1));
        var nextReturnAxis = Axis(nextReturnStamps.Last(stamp =>
            stamp.AccumulationGroup == 1));

        True(
            Vector2.Dot(outwardAxis, firstReturnAxis) > 0,
            $"{shape} の非整除折り返し1フレーム目を上下反転してはいけません。");
        True(
            Vector2.Dot(firstReturnAxis, nextReturnAxis) > 0,
            $"{shape} の非整除折り返し1・2フレーム間で上下反転してはいけません。");
    }

    foreach (var shape in new[]
             {
                 GlassWipeBrushShape.Ellipse,
             })
    {
        True(
            !WipeBrushStampGenerator.RequiresReturnTangentCorrection(shape, 1),
            $"{shape} の既存回転追従へ往復補正を適用してはいけません。");
    }
}

static (int Width, int Height) GetMaskBounds(WipeBrushBinaryMask mask)
{
    var minX = mask.Size;
    var minY = mask.Size;
    var maxX = -1;
    var maxY = -1;
    for (var y = 0; y < mask.Size; y++)
    {
        for (var x = 0; x < mask.Size; x++)
        {
            if (!mask.Pixels[y * mask.Size + x])
            {
                continue;
            }

            minX = Math.Min(minX, x);
            minY = Math.Min(minY, y);
            maxX = Math.Max(maxX, x);
            maxY = Math.Max(maxY, y);
        }
    }

    True(maxX >= minX && maxY >= minY, "ブラシマスクは有効画素を持つ必要があります。");
    return (maxX - minX + 1, maxY - minY + 1);
}

static float GetLowerPalmWidthRatio(WipeBrushBinaryMask mask)
{
    var bounds = GetMaskBounds(mask);
    var startY = (int)MathF.Floor(mask.Size * 0.8f);
    var endY = (int)MathF.Ceiling(mask.Size * 0.95f);
    var totalWidth = 0;
    var rowCount = 0;
    for (var y = startY; y < endY; y++)
    {
        var minX = mask.Size;
        var maxX = -1;
        for (var x = 0; x < mask.Size; x++)
        {
            if (!mask.Pixels[y * mask.Size + x])
            {
                continue;
            }

            minX = Math.Min(minX, x);
            maxX = Math.Max(maxX, x);
        }

        if (maxX >= minX)
        {
            totalWidth += maxX - minX + 1;
            rowCount++;
        }
    }

    return rowCount > 0 && bounds.Width > 0
        ? totalWidth / (float)rowCount / bounds.Width
        : 0;
}

static void VerifyEllipseBrushRotationInterpolation()
{
    var geometry = WipeMaskGeometry.Create(1000, 1000);
    var stamps = WipeBrushStampGenerator.Generate(
        [
            Sample(
                0,
                0.4f,
                0.5f,
                1,
                0.4f,
                aspectRatio: 2,
                rotationRadians: 170 * MathF.PI / 180),
            Sample(
                1,
                0.59f,
                0.5f,
                1,
                0.4f,
                aspectRatio: 2,
                rotationRadians: -170 * MathF.PI / 180),
        ],
        0,
        geometry);

    Equal(3, stamps.Count, "回転補間を確認する中間スタンプ数が不正です。");
    var middleAngle = MathF.Atan2(
        stamps[1].Transform.M12,
        stamps[1].Transform.M11);
    NearlyEqual(
        MathF.PI,
        MathF.Abs(middleAngle),
        "170°から-170°は0°経由ではなく最短方向で補間する必要があります。",
        0.00001f);
}

static void VerifyPathSmoothing()
{
    var geometry = WipeMaskGeometry.Create(1920, 1080);
    var cornerSamples = new[]
    {
        Sample(0, 0.1f, 0.2f, 1),
        Sample(1, 0.5f, 0.2f, 1),
        Sample(2, 0.5f, 0.6f, 1),
    };
    var legacy = WipeBrushStampGenerator.Generate(
        cornerSamples,
        0,
        geometry);
    var explicitlyDisabled = WipeBrushStampGenerator.Generate(
        cornerSamples,
        0,
        geometry,
        new WipePathStyle(0, 0, 9999));
    True(
        legacy.SequenceEqual(explicitlyDisabled),
        "平滑化・微小揺れ0%では従来のスタンプ列を完全に維持する必要があります。");

    var smoothed = WipeBrushStampGenerator.Generate(
        cornerSamples,
        0,
        geometry,
        new WipePathStyle(1, 0, 1));
    Vector2Equal(
        new Vector2(0.5f, 0.6f) * WipeMaskRenderer.MaskSize,
        smoothed[^1].Center,
        "平滑化で軌跡の終点が移動してはいけません。",
        0.0001f);
    True(
        smoothed.Any(stamp =>
            stamp.Center.X > 0.5001f * WipeMaskRenderer.MaskSize),
        "直角の軌跡で平滑化による曲線が生成されていません。");

    var splitSamples = new[]
    {
        Sample(0, 0.1f, 0.2f, 1),
        Sample(1, 0.3f, 0.2f, 1),
        Sample(2, 0.6f, 0.6f, 0),
        Sample(3, 0.9f, 0.9f, 1),
    };
    var firstStroke = WipeBrushStampGenerator.Generate(
        splitSamples[..2],
        0,
        geometry,
        new WipePathStyle(1, 0, 1));
    var split = WipeBrushStampGenerator.Generate(
        splitSamples,
        0,
        geometry,
        new WipePathStyle(1, 0, 1));
    Equal(
        firstStroke.Count + 1,
        split.Count,
        "接触率0をまたいで平滑化軌跡を接続してはいけません。");
    Vector2Equal(
        new Vector2(0.9f, 0.9f) * WipeMaskRenderer.MaskSize,
        split[^1].Center,
        "接触再開時は新しいストロークの始点を維持する必要があります。",
        0.0001f);

    var reversal = WipeBrushStampGenerator.Generate(
        [
            Sample(0, 0.2f, 0.5f, 1, 0.11f),
            Sample(1, 0.8f, 0.5f, 1, 0.11f),
            Sample(2, 0.3f, 0.5f, 1, 0.11f),
        ],
        0,
        geometry,
        new WipePathStyle(1, 0, 1));
    True(
        reversal.All(stamp =>
            MathF.Abs(
                stamp.Center.Y / WipeMaskRenderer.MaskSize - 0.5f) <=
            0.00001f),
        "急激な折り返しを過剰に丸めて軌跡外へ膨らませてはいけません。");
}

static void VerifyDeterministicJitter()
{
    var geometry = WipeMaskGeometry.Create(1920, 1080);
    var samples = new[]
    {
        Sample(0, 0.1f, 0.5f, 1),
        Sample(1, 0.5f, 0.5f, 1),
        Sample(2, 0.9f, 0.65f, 1),
    };
    var style = new WipePathStyle(0.75f, 0.02f, 42);
    var first = WipeBrushStampGenerator.Generate(samples, 0, geometry, style);
    var second = WipeBrushStampGenerator.Generate(samples, 0, geometry, style);
    True(
        first.SequenceEqual(second),
        "同じSeed・設定・軌跡から異なる微小揺れが生成されました。");

    var changedSeed = WipeBrushStampGenerator.Generate(
        samples,
        0,
        geometry,
        new WipePathStyle(0.75f, 0.02f, 43));
    Equal(first.Count, changedSeed.Count, "Seed変更でスタンプ数が変化してはいけません。");
    True(
        first.Where((stamp, index) =>
            stamp.Center != changedSeed[index].Center).Any(),
        "Seed変更後も微小揺れの形が変化していません。");

    var noJitter = WipeBrushStampGenerator.Generate(
        samples,
        0,
        geometry,
        new WipePathStyle(0.75f, 0, 42));
    Equal(first.Count, noJitter.Count, "微小揺れの有無でスタンプ数が変化してはいけません。");
    for (var index = 0; index < first.Count; index++)
    {
        var localOffset =
            (first[index].Center - noJitter[index].Center) /
            WipeMaskRenderer.MaskSize;
        var offset = geometry.ToShortSideVector(localOffset).Length();
        True(
            offset <= style.Jitter + 0.00001f,
            "微小揺れが指定した最大幅を超えています。");
    }
}

static void VerifyQualitySpacingAndDefaults()
{
    var geometry = WipeMaskGeometry.Create(1000, 1000);
    var samples = new[]
    {
        Sample(0, 0.1f, 0.5f, 1, 0.1f),
        Sample(1, 0.9f, 0.5f, 1, 0.1f),
    };
    var legacy = WipeBrushStampGenerator.Generate(samples, 0, geometry);
    var standard = WipeBrushStampGenerator.Generate(
        samples,
        0,
        geometry,
        new WipePathStyle(
            0,
            0,
            9999,
            0,
            0,
            GlassWipeQuality.Standard));
    var low = WipeBrushStampGenerator.Generate(
        samples,
        0,
        geometry,
        new WipePathStyle(0, 0, 1, 0, 0, GlassWipeQuality.Low));
    var high = WipeBrushStampGenerator.Generate(
        samples,
        0,
        geometry,
        new WipePathStyle(0, 0, 1, 0, 0, GlassWipeQuality.High));

    True(
        legacy.SequenceEqual(standard),
        "標準品質・新設定0では従来のスタンプ列を完全に維持する必要があります。");
    True(low.Count <= standard.Count, "軽量品質で標準品質よりスタンプが増えています。");
    True(high.Count > standard.Count, "高品質で標準品質よりスタンプが増えていません。");
    Equal(
        GlassWipeQuality.Standard,
        GlassWipeParameterSanitizer.SanitizeQuality((GlassWipeQuality)99),
        "不正な品質値は標準へ補正する必要があります。");
}

static void VerifySimpleGeneratedQuality()
{
    Equal(0, (int)GlassWipeSimpleGeneratedQuality.Auto, "定型軌跡の自動品質値");
    Equal(1, (int)GlassWipeSimpleGeneratedQuality.Low, "定型軌跡の軽量品質値");
    Equal(2, (int)GlassWipeSimpleGeneratedQuality.Standard, "定型軌跡の標準品質値");
    Equal(3, (int)GlassWipeSimpleGeneratedQuality.High, "定型軌跡の高品質値");
    Equal(4, Enum.GetValues<GlassWipeSimpleGeneratedQuality>().Length, "定型軌跡の品質数");
    Equal("自動", GetDisplayName(GlassWipeSimpleGeneratedQuality.Auto), "自動品質の表示名");
    Equal("軽量", GetDisplayName(GlassWipeSimpleGeneratedQuality.Low), "軽量品質の表示名");
    Equal("標準", GetDisplayName(GlassWipeSimpleGeneratedQuality.Standard), "標準品質の表示名");
    Equal("高品質", GetDisplayName(GlassWipeSimpleGeneratedQuality.High), "高品質の表示名");
    Equal(
        GlassWipeSimpleGeneratedQuality.Auto,
        GlassWipeSimpleGeneratedQualityCompatibility.Normalize(
            (GlassWipeSimpleGeneratedQuality)99),
        "不正な定型軌跡品質は自動へ補正する必要があります。");

    WipeSimplePathGenerationResult Generate(
        GlassWipeSimplePattern pattern,
        GlassWipeSimpleGeneratedQuality quality) =>
        WipeSimplePathGenerator.Generate(
            pattern,
            GlassWipePathInterpolation.Linear,
            new Vector2(10, 50),
            new Vector2(50, 20),
            new Vector2(90, 50),
            2,
            90,
            120,
            simpleGeneratedQuality: quality);

    foreach (var pattern in new[]
    {
        GlassWipeSimplePattern.StraightOnce,
        GlassWipeSimplePattern.GentleArcOnce,
    })
    {
        var generated = Generate(pattern, GlassWipeSimpleGeneratedQuality.Auto);
        Equal(
            GlassWipeQuality.Standard,
            generated.Style.Quality,
            $"{pattern}の自動品質は標準である必要があります。");
        Equal(
            WipeMaskAccumulationMode.SourceOver,
            generated.Style.AccumulationMode,
            $"{pattern}の累積方式を品質選択で変更してはいけません。");
    }

    foreach (var pattern in new[]
    {
        GlassWipeSimplePattern.ShortRoundTrips,
        GlassWipeSimplePattern.ArcRoundTrips,
        GlassWipeSimplePattern.OffsetRoundTrips,
    })
    {
        var generated = Generate(pattern, GlassWipeSimpleGeneratedQuality.Auto);
        Equal(
            GlassWipeQuality.High,
            generated.Style.Quality,
            $"{pattern}の自動品質は高品質である必要があります。");
        Equal(
            WipeMaskAccumulationMode.PerPassMaximum,
            generated.Style.AccumulationMode,
            $"{pattern}の片道単位累積を品質選択で変更してはいけません。");
    }

    var explicitQualities = new[]
    {
        (GlassWipeSimpleGeneratedQuality.Low, GlassWipeQuality.Low),
        (GlassWipeSimpleGeneratedQuality.Standard, GlassWipeQuality.Standard),
        (GlassWipeSimpleGeneratedQuality.High, GlassWipeQuality.High),
    };
    foreach (var (selected, expected) in explicitQualities)
    {
        Equal(
            expected,
            Generate(GlassWipeSimplePattern.StraightOnce, selected).Style.Quality,
            $"一回拭きの明示品質{selected}");
        var roundTrip = Generate(GlassWipeSimplePattern.ShortRoundTrips, selected);
        Equal(expected, roundTrip.Style.Quality, $"往復拭きの明示品質{selected}");
        Equal(
            WipeMaskAccumulationMode.PerPassMaximum,
            roundTrip.Style.AccumulationMode,
            "明示品質でも往復拭きの累積方式を維持する必要があります。");
    }

    var auto = Generate(
        GlassWipeSimplePattern.StraightOnce,
        GlassWipeSimpleGeneratedQuality.Auto);
    var low = Generate(
        GlassWipeSimplePattern.StraightOnce,
        GlassWipeSimpleGeneratedQuality.Low);
    var standard = Generate(
        GlassWipeSimplePattern.StraightOnce,
        GlassWipeSimpleGeneratedQuality.Standard);
    var high = Generate(
        GlassWipeSimplePattern.StraightOnce,
        GlassWipeSimpleGeneratedQuality.High);
    True(auto.Signature.StartsWith("simple:v9|", StringComparison.Ordinal), "定型軌跡署名はv9である必要があります。");
    Equal(auto.Signature, standard.Signature, "自動で標準へ解決した署名は明示標準と一致する必要があります。");
    True(low.Signature != standard.Signature, "軽量と標準の署名を区別する必要があります。");
    True(high.Signature != standard.Signature, "高品質と標準の署名を区別する必要があります。");

    var geometry = WipeMaskGeometry.Create(1000, 1000);
    WipeSimplePathGenerationResult GenerateSparse(
        GlassWipeSimpleGeneratedQuality quality) =>
        WipeSimplePathGenerator.Generate(
            GlassWipeSimplePattern.StraightOnce,
            GlassWipePathInterpolation.Linear,
            new Vector2(10, 50),
            new Vector2(50, 50),
            new Vector2(90, 50),
            2,
            1,
            1,
            simpleGeneratedQuality: quality);
    var sparseLow = GenerateSparse(GlassWipeSimpleGeneratedQuality.Low);
    var sparseStandard = GenerateSparse(GlassWipeSimpleGeneratedQuality.Standard);
    var sparseHigh = GenerateSparse(GlassWipeSimpleGeneratedQuality.High);
    var lowStamps = WipeBrushStampGenerator.Generate(
        sparseLow.Samples,
        0,
        geometry,
        sparseLow.Style);
    var standardStamps = WipeBrushStampGenerator.Generate(
        sparseStandard.Samples,
        0,
        geometry,
        sparseStandard.Style);
    var highStamps = WipeBrushStampGenerator.Generate(
        sparseHigh.Samples,
        0,
        geometry,
        sparseHigh.Style);
    True(lowStamps.Count <= standardStamps.Count, "定型軌跡の軽量品質でスタンプが増えています。");
    True(highStamps.Count > standardStamps.Count, "定型軌跡の高品質でスタンプが増えていません。");

    var lowSnapshot = low.CreateSnapshot(60);
    var highSnapshot = high.CreateSnapshot(60);
    True(lowSnapshot.Fingerprint != highSnapshot.Fingerprint, "品質をSnapshot Fingerprintへ含める必要があります。");
    Equal(
        WipeMaskUpdateKind.Rebuild,
        WipeMaskUpdatePlan.Create(lowSnapshot, geometry, highSnapshot, geometry).Kind,
        "定型軌跡の品質変更後はMaskを再構築する必要があります。");

    Equal(17, GetPrivateConstant<int>(typeof(WipePathSnapshot), "SchemaVersion"), "Path Snapshot schema");
    Equal(
        20,
        GetPrivateConstant<int>(typeof(WipePathDefinitionFingerprint), "SchemaVersion"),
        "Definition Fingerprint schema");

    var repositoryRoot = FindRepositoryRoot();
    var streamSource = File.ReadAllText(
        Path.Combine(repositoryRoot, "src", "YMM4GlassWipe", "WipePathStream.cs"));
    var snapshotSource = File.ReadAllText(
        Path.Combine(repositoryRoot, "src", "YMM4GlassWipe", "WipePathSnapshot.cs"));
    var definitionSource = File.ReadAllText(
        Path.Combine(
            repositoryRoot,
            "src",
            "YMM4GlassWipe",
            "WipePathDefinitionFingerprint.cs"));
    True(
        streamSource.Contains("item.SimpleGeneratedQuality", StringComparison.Ordinal),
        "本番Stream経路へ定型軌跡品質を渡す必要があります。");
    True(
        snapshotSource.Contains("item.SimpleGeneratedQuality", StringComparison.Ordinal),
        "Snapshot経路へ定型軌跡品質を渡す必要があります。");
    True(
        definitionSource.Contains("builder.Add(style);", StringComparison.Ordinal) &&
        definitionSource.Contains("Add((int)style.Quality);", StringComparison.Ordinal) &&
        definitionSource.Contains(
            "builder.AddAnimation(item.PngBrushSizeScale);",
            StringComparison.Ordinal),
        "解決後の品質とPNG倍率をDefinition Fingerprintへ含める必要があります。");
}

static void VerifyBrushRotationFollow()
{
    static float GetStampAngle(WipeBrushStamp stamp) =>
        MathF.Atan2(stamp.Transform.M12, stamp.Transform.M11);

    var geometry = WipeMaskGeometry.Create(1000, 1000);
    var samples = new[]
    {
        Sample(0, 0.5f, 0.2f, 1, 0.1f, aspectRatio: 2),
        Sample(1, 0.5f, 0.8f, 1, 0.1f, aspectRatio: 2),
    };
    var followed = WipeBrushStampGenerator.Generate(
        samples,
        0,
        geometry,
        new WipePathStyle(0, 0, 1, 1));
    var followedAngle = MathF.Atan2(
        followed[^1].Transform.M12,
        followed[^1].Transform.M11);
    NearlyEqual(
        MathF.PI / 2,
        followedAngle,
        "100%回転追従では楕円長径を移動方向へ向ける必要があります。",
        0.00001f);

    var offsetSamples = new[]
    {
        Sample(0, 0.5f, 0.2f, 1, 0.1f, aspectRatio: 2, rotationRadians: MathF.PI / 4),
        Sample(1, 0.5f, 0.8f, 1, 0.1f, aspectRatio: 2, rotationRadians: MathF.PI / 4),
    };
    var offset = WipeBrushStampGenerator.Generate(
        offsetSamples,
        0,
        geometry,
        new WipePathStyle(0, 0, 1, 1));
    var offsetAngle = MathF.Atan2(
        offset[^1].Transform.M12,
        offset[^1].Transform.M11);
    NearlyEqual(
        MathF.PI * 0.75f,
        offsetAngle,
        "手動回転角は移動方向への追従角へ加算する必要があります。",
        0.00001f);

    var isolatedSample = Sample(
        0,
        0.5f,
        0.2f,
        1,
        0.1f,
        aspectRatio: 2,
        rotationRadians: MathF.PI / 4);
    var isolatedFollowed = WipeBrushStampGenerator.Generate(
        [isolatedSample],
        0,
        geometry,
        new WipePathStyle(
            0,
            0,
            1,
            1,
            InitialTangentY: 1));
    NearlyEqual(
        MathF.PI * 0.75f,
        GetStampAngle(isolatedFollowed.Single()),
        "単独の開始スタンプにも開始方向と手動回転角を適用する必要があります。",
        0.00001f);
    var isolatedUnfollowed = WipeBrushStampGenerator.Generate(
        [isolatedSample],
        0,
        geometry,
        new WipePathStyle(
            0,
            0,
            1,
            0,
            InitialTangentY: 1));
    NearlyEqual(
        MathF.PI / 4,
        GetStampAngle(isolatedUnfollowed.Single()),
        "回転追従0%では開始方向を適用してはいけません。",
        0.00001f);

    var simpleCases = new[]
    {
        (GlassWipeSimplePattern.StraightOnce, GlassWipePathInterpolation.Linear),
        (GlassWipeSimplePattern.GentleArcOnce, GlassWipePathInterpolation.Smooth),
        (GlassWipeSimplePattern.GentleArcOnce, GlassWipePathInterpolation.CircularArc),
        (GlassWipeSimplePattern.ShortRoundTrips, GlassWipePathInterpolation.Linear),
        (GlassWipeSimplePattern.ArcRoundTrips, GlassWipePathInterpolation.CircularArc),
        (GlassWipeSimplePattern.OffsetRoundTrips, GlassWipePathInterpolation.CircularArc),
    };
    foreach (var (pattern, interpolation) in simpleCases)
    {
        var frame0 = WipeSimplePathGenerator.Generate(
            pattern,
            interpolation,
            new Vector2(15, 85),
            new Vector2(50, 45),
            new Vector2(100, 25),
            2,
            0,
            600,
            maskGeometry: geometry);
        var frame1 = WipeSimplePathGenerator.Generate(
            pattern,
            interpolation,
            new Vector2(15, 85),
            new Vector2(50, 45),
            new Vector2(100, 25),
            2,
            1,
            600,
            maskGeometry: geometry);
        var style0 = WipePathSnapshot.ResolveSimpleGeneratedStyle(
            GlassWipeEditingMode.Simple,
            frame0.Style,
            GlassWipeBrushShape.Hand,
            100);
        var style1 = WipePathSnapshot.ResolveSimpleGeneratedStyle(
            GlassWipeEditingMode.Simple,
            frame1.Style,
            GlassWipeBrushShape.Hand,
            100);
        var samples0 = WipePathSnapshot.ResolveSimpleGeneratedSamples(
            GlassWipeEditingMode.Simple,
            frame0.Samples,
            _ => new WipeBrushPixelDimensions(230, 230),
            _ => -140);
        var samples1 = WipePathSnapshot.ResolveSimpleGeneratedSamples(
            GlassWipeEditingMode.Simple,
            frame1.Samples,
            _ => new WipeBrushPixelDimensions(230, 230),
            _ => -140);
        var stamps0 = WipeBrushStampGenerator.Generate(
            samples0,
            0,
            geometry,
            style0);
        var stamps1 = WipeBrushStampGenerator.Generate(
            samples1,
            0,
            geometry,
            style1);
        True(
            stamps0.Count > 0 && stamps1.Count > 0,
            $"{pattern}/{interpolation} の開始スタンプを生成する必要があります。");
        NearlyEqual(
            GetStampAngle(stamps0[0]),
            GetStampAngle(stamps1[0]),
            $"{pattern}/{interpolation} の1フレーム目と2フレーム目で開始角度を維持する必要があります。",
            0.00001f);
    }
}

static void VerifyBrushSoftness()
{
    var geometry = WipeMaskGeometry.Create(1000, 1000);
    var style = new WipePathStyle(0, 0, 1, 0, 0.65f);
    var stamps = WipeBrushStampGenerator.Generate(
        [Sample(0, 0.5f, 0.5f, 1)],
        0,
        geometry,
        style);

    True(stamps.Count > 0, "柔らかいブラシのスタンプが生成されていません。");
    True(
        stamps.All(stamp => MathF.Abs(stamp.Softness - 0.65f) <= 0.000001f),
        "ブラシ柔らかさがスタンプへ保持されていません。");
    NearlyEqual(
        0,
        new WipePathStyle(0, 0, 1, 0, float.PositiveInfinity).Sanitize().Softness,
        "非有限の柔らかさは0へ補正する必要があります。");
    True(!style.Sanitize().IsDisabled, "柔らかさが有効なスタイルを従来経路へ送ってはいけません。");
}

static void VerifyResidueAndNoiseDeterminism()
{
    var coordinate = new Vector2(0.37f, 0.61f);
    var first = EffectiveWipeMaskReference(0.8f, 0.25f, 0.7f, coordinate, 42);
    var second = EffectiveWipeMaskReference(0.8f, 0.25f, 0.7f, coordinate, 42);
    var changedSeed = EffectiveWipeMaskReference(0.8f, 0.25f, 0.7f, coordinate, 43);

    NearlyEqual(first, second, "同じ座標とSeedで拭きムラが変化しています。");
    True(first <= 0.8f * 0.75f + 0.000001f, "拭き残しで指定した最大除去率を超えています。");
    True(MathF.Abs(first - changedSeed) > 0.000001f, "Seed変更後も拭きムラが変化していません。");
    NearlyEqual(
        0.8f,
        EffectiveWipeMaskReference(0.8f, 0, 0, coordinate, 42),
        "拭き残し・拭きムラ0では元のマスク値を維持する必要があります。");

    var fogFirst = FogNoiseOffsetReference(coordinate, 42, 1);
    var fogSecond = FogNoiseOffsetReference(coordinate, 42, 1);
    var fogChangedSeed = FogNoiseOffsetReference(coordinate, 43, 1);
    NearlyEqual(fogFirst, fogSecond, "同じ座標とSeedで曇りノイズが変化しています。");
    True(MathF.Abs(fogFirst) <= 0.120001f, "曇りノイズが設計上の最大振幅を超えています。");
    True(MathF.Abs(fogFirst - fogChangedSeed) > 0.000001f, "Seed変更後も曇りノイズが変化していません。");
}

static void VerifyDuplicatePointAndZeroDuration()
{
    var geometry = WipeMaskGeometry.Create(1920, 1080);
    var stamps = WipeBrushStampGenerator.Generate(
        [
            Sample(0, 0.5f, 0.5f, 1),
            Sample(1, 0.5f, 0.5f, 1),
        ],
        0,
        geometry);

    True(stamps.Count > 0, "重複点でも開始スタンプを維持する必要があります。");
    True(
        stamps.All(stamp =>
            float.IsFinite(stamp.Center.X) &&
            float.IsFinite(stamp.Center.Y) &&
            float.IsFinite(stamp.RadiusX) &&
            float.IsFinite(stamp.RadiusY) &&
            MatrixIsFinite(stamp.Transform) &&
            float.IsFinite(stamp.Alpha)),
        "重複点から非有限値が生成されました。");
    var zeroSizeStamps = WipeBrushStampGenerator.Generate(
        [
            Sample(0, 0, 0, 1, 0),
            Sample(1, 1, 1, 1, 0),
        ],
        0,
        geometry);
    Equal(0, zeroSizeStamps.Count, "サイズ0の線分はスタンプを生成してはいけません。");
    Equal(0, WipePathSnapshot.GetLastHistoryFrame(-1, 0), "負のフレームは0へ補正します。");
    Equal(0, WipePathSnapshot.GetLastHistoryFrame(100, 0), "長さ0では履歴フレーム0だけを評価します。");
    Equal(10, WipePathSnapshot.GetLastHistoryFrame(100, 10), "現在位置はアイテム長へClampします。");
}

static void VerifyStrokeDocumentCodec()
{
    Equal(
        WipePathInputMode.LegacyAnimation,
        default(WipePathInputMode),
        "旧キーフレーム方式を既定値として維持する必要があります。");

    var document = new WipeStrokeDocument
    {
        Strokes =
        [
            new WipeStroke
            {
                Points =
                [
                    PercentStrokePoint(0, 10, 20, 100),
                    PercentStrokePoint(25, 30, 40, 75),
                ],
            },
            new WipeStroke
            {
                Points =
                [
                    PercentStrokePoint(50, 70, 80, 100),
                    PercentStrokePoint(100, 90, 60, 0),
                ],
            },
        ],
    };

    var encoded = WipeStrokeDocumentCodec.Encode(document);
    True(
        encoded.StartsWith(WipeStrokeDocumentCodec.Prefix, StringComparison.Ordinal),
        "保存データにスキーマ接頭辞がありません。");
    Equal(
        encoded,
        WipeStrokeDocumentCodec.Encode(document),
        "同じカスタム軌跡を決定的に保存する必要があります。");
    True(
        WipeStrokeDocumentCodec.TryDecode(encoded, out var decoded),
        "正しいカスタム軌跡を復元できません。");
    Equal(2, decoded.Strokes.Count, "Stroke数が保存後に変化しました。");
    Equal(2, decoded.Strokes[0].Points.Count, "制御点数が保存後に変化しました。");
    Equal(75d, decoded.Strokes[0].Points[1].Contact, "接触率が保存後に変化しました。");
    Equal(
        encoded,
        WipeStrokeDocumentCodec.Encode(decoded),
        "読み書き後に保存形式が変化しました。");

    var reordered = new WipeStrokeDocument
    {
        Strokes =
        [
            new WipeStroke
            {
                Points =
                [
                    PercentStrokePoint(80, 10, 20, 100),
                    PercentStrokePoint(20, 30, 40, 75),
                    PercentStrokePoint(20, 50, 60, 50),
                ],
            },
        ],
    };
    True(
        WipeStrokeDocumentCodec.TryDecode(
            WipeStrokeDocumentCodec.Encode(reordered),
            out var reorderedDecoded),
        "並べ替えた制御点を復元できません。");
    Equal(80d, reorderedDecoded.Strokes[0].Points[0].TimelinePercent, "制御点順が変化しました。");
    Equal(20d, reorderedDecoded.Strokes[0].Points[1].TimelinePercent, "制御点順が変化しました。");
    Equal(20d, reorderedDecoded.Strokes[0].Points[2].TimelinePercent, "同一時刻点の順序が変化しました。");

    var outOfRange = WipeStrokeDocumentCodec.Prefix +
        "{\"dataSchemaVersion\":2,\"strokes\":[{\"points\":[{\"timelinePercent\":-25,\"x\":2500,\"y\":-10,\"contact\":150}]}]}";
    True(
        WipeStrokeDocumentCodec.TryDecode(outOfRange, out var clamped),
        "有限な範囲外値を安全に補正できません。");
    Equal(0d, clamped.Strokes[0].Points[0].TimelinePercent, "時刻の下限補正が不正です。");
    Equal(1920d, clamped.Strokes[0].Points[0].X, "Xの上限補正が不正です。");
    Equal(0d, clamped.Strokes[0].Points[0].Y, "Yの下限補正が不正です。");
    Equal(100d, clamped.Strokes[0].Points[0].Contact, "接触率の上限補正が不正です。");

    var cloned = decoded.DeepClone();
    decoded.Strokes[0].Points[0].X = 99;
    Equal(192d, cloned.Strokes[0].Points[0].X, "DeepCloneが元データを共有しています。");

    var invalidValues = new[]
    {
        string.Empty,
        "broken",
        WipeStrokeDocumentCodec.Prefix + "{\"dataSchemaVersion\":3,\"strokes\":[]}",
        WipeStrokeDocumentCodec.Prefix + "{\"dataSchemaVersion\":1,\"strokes\":[],\"unknown\":true}",
        WipeStrokeDocumentCodec.Prefix + "{\"dataSchemaVersion\":1,\"strokes\":[{\"points\":[{\"timelinePercent\":1e400,\"x\":0,\"y\":0,\"contact\":100}]}]}",
        "v1:{\"dataSchemaVersion\":1,\"strokes\":[]}",
    };
    foreach (var invalidValue in invalidValues)
    {
        True(
            !WipeStrokeDocumentCodec.TryDecode(invalidValue, out var rejected),
            "不正または将来版の保存データを受理してはいけません。");
        Equal(0, rejected.Strokes.Count, "復元失敗時は空の安全な結果を返す必要があります。");
    }

    var nullSafe = new WipeStrokeDocument { Strokes = null! }.Sanitize();
    Equal(0, nullSafe.Strokes.Count, "null Stroke一覧を安全に空へ補正する必要があります。");
    var nullPointSafe = new WipeStroke { Points = null! }.Sanitize();
    Equal(0, nullPointSafe.Points.Count, "null制御点一覧を安全に空へ補正する必要があります。");
}

static void VerifyCentripetalCatmullRom()
{
    var point0 = new Vector2(0, 0);
    var point1 = new Vector2(0.2f, 0.8f);
    var point2 = new Vector2(0.8f, 0.2f);
    var point3 = new Vector2(1, 1);

    Vector2Equal(
        point1,
        CentripetalCatmullRom.Evaluate(point0, point1, point2, point3, 0),
        "Catmull-Romの開始点が一致しません。");
    Vector2Equal(
        point2,
        CentripetalCatmullRom.Evaluate(point0, point1, point2, point3, 1),
        "Catmull-Romの終了点が一致しません。");

    for (var index = 0; index <= 100; index++)
    {
        var value = CentripetalCatmullRom.Evaluate(
            point0,
            point1,
            point2,
            point3,
            index / 100f);
        True(
            float.IsFinite(value.X) && float.IsFinite(value.Y),
            "Catmull-Rom補間から非有限値が生成されました。");
    }

    var duplicate = CentripetalCatmullRom.Evaluate(
        point1,
        point1,
        point1,
        point1,
        0.5f);
    Vector2Equal(point1, duplicate, "重複制御点のフォールバックが不正です。");

    var invalid = CentripetalCatmullRom.Evaluate(
        new Vector2(float.NaN, 0),
        new Vector2(0.25f, 0.5f),
        new Vector2(0.75f, 0.5f),
        Vector2.One,
        0.5f);
    Vector2Equal(new Vector2(0.5f, 0.5f), invalid, "非有限入力を安全に線形補間できません。");
}

static void VerifyCustomStrokeSampling()
{
    var document = new WipeStrokeDocument
    {
        Strokes =
        [
            new WipeStroke
            {
                Points =
                [
                    PercentStrokePoint(0, 10, 20, 100),
                    PercentStrokePoint(20, 30, 20, 100),
                ],
            },
            new WipeStroke
            {
                Points =
                [
                    PercentStrokePoint(40, 70, 80, 100),
                    PercentStrokePoint(60, 90, 80, 100),
                ],
            },
        ],
    };

    var samples = WipeStrokeSampler.Expand(document, 100, 100);
    var boundaryIndex = Array.FindIndex(
        samples,
        sample => sample.Frame == 40 && sample.Contact == 0);
    True(boundaryIndex > 0, "Stroke間の接触率0境界がありません。");
    True(boundaryIndex + 1 < samples.Length, "次のStrokeの開始点がありません。");
    Equal(40, samples[boundaryIndex + 1].Frame, "Stroke境界のフレームが変化しました。");
    NearlyEqual(0.7f, samples[boundaryIndex].X, "Stroke境界のXが不正です。");
    NearlyEqual(0.8f, samples[boundaryIndex].Y, "Stroke境界のYが不正です。");
    NearlyEqual(1, samples[boundaryIndex + 1].Contact, "次のStrokeが接触状態で開始しません。");
    True(
        samples.Take(boundaryIndex).All(sample => sample.Frame <= 20) &&
        samples.Skip(boundaryIndex + 1).All(sample => sample.Frame >= 40),
        "別Stroke間を補間してはいけません。");

    var singlePoint = new WipeStrokeDocument
    {
        Strokes =
        [
            new WipeStroke
            {
                Points = [PercentStrokePoint(50, 25, 75, 100)],
            },
        ],
    };
    var singleSamples = WipeStrokeSampler.Expand(singlePoint, 100, 100);
    Equal(1, singleSamples.Length, "単一点Strokeを維持する必要があります。");
    Equal(50, singleSamples[0].Frame, "単一点Strokeのフレームが不正です。");

    var threePoint = new WipeStrokeDocument
    {
        Strokes =
        [
            new WipeStroke
            {
                Points =
                [
                    PercentStrokePoint(0, 10, 50, 100),
                    PercentStrokePoint(50, 50, 10, 75),
                    PercentStrokePoint(100, 90, 50, 50),
                ],
            },
        ],
    };
    var threePointSamples = WipeStrokeSampler.Expand(threePoint, 100, 100);
    Equal(0, threePointSamples[0].Frame, "3点Strokeの開始フレームが不正です。");
    Equal(100, threePointSamples[^1].Frame, "3点Strokeの終了フレームが不正です。");
    True(
        threePointSamples.All(sample =>
            float.IsFinite(sample.X) &&
            float.IsFinite(sample.Y) &&
            float.IsFinite(sample.Contact)),
        "3点Strokeの補間から非有限値が生成されました。");

    var sameEffectiveFrame = new WipeStrokeDocument
    {
        Strokes =
        [
            new WipeStroke
            {
                Points =
                [
                    PercentStrokePoint(80, 10, 50, 100),
                    PercentStrokePoint(20, 50, 50, 75),
                    PercentStrokePoint(20, 90, 50, 50),
                ],
            },
        ],
    };
    var sameFrameSamples = WipeStrokeSampler.Expand(sameEffectiveFrame, 100, 100);
    Equal(3, sameFrameSamples.Length, "同一実効フレームの制御点を削除してはいけません。");
    True(sameFrameSamples.All(sample => sample.Frame == 80), "実効フレームの単調化が不正です。");
    NearlyEqual(0.1f, sameFrameSamples[0].X, "保存順の先頭点が変化しました。");
    NearlyEqual(0.5f, sameFrameSamples[1].X, "保存順の中間点が変化しました。");
    NearlyEqual(0.9f, sameFrameSamples[2].X, "保存順の終端点が変化しました。");

    var regressiveStrokes = new WipeStrokeDocument
    {
        Strokes =
        [
            new WipeStroke
            {
                Points =
                [
                    PercentStrokePoint(60, 10, 30, 100),
                    PercentStrokePoint(80, 40, 30, 100),
                ],
            },
            new WipeStroke
            {
                Points =
                [
                    PercentStrokePoint(10, 60, 70, 100),
                    PercentStrokePoint(30, 90, 70, 100),
                ],
            },
        ],
    };
    var regressiveSamples = WipeStrokeSampler.Expand(regressiveStrokes, 100, 100);
    var regressiveBoundary = Array.FindIndex(
        regressiveSamples,
        sample => sample.Frame == 10 && sample.Contact == 0);
    True(regressiveBoundary > 0, "後続Strokeの分離境界がありません。");
    True(
        regressiveSamples[regressiveBoundary - 1].Frame >
        regressiveSamples[regressiveBoundary].Frame,
        "Stroke順を時刻順へ並べ替えてはいけません。");
    True(
        regressiveSamples.SequenceEqual(
            WipeStrokeSampler.Expand(regressiveStrokes, 100, 100)),
        "時刻が逆行する複数Strokeの評価が決定的ではありません。");

    Equal(
        0,
        WipeStrokeSampler.Expand(WipeStrokeDocument.CreateEmpty(), 100, 100).Length,
        "空のカスタム軌跡はサンプルを生成してはいけません。");
    var futureDocument = WipeStrokeDocument.CreateStarter();
    futureDocument.DataSchemaVersion = WipeStrokeDocument.CurrentVersion + 1;
    Equal(
        0,
        WipeStrokeSampler.Expand(futureDocument, 100, 100).Length,
        "将来版のカスタム軌跡を直接評価してはいけません。");
}

static void VerifyPathInputFallback()
{
    var encoded = WipeStrokeDocumentCodec.Encode(WipeStrokeDocument.CreateStarter());
    True(
        !WipePathSnapshot.TryResolveCustomDocument(
            WipePathInputMode.LegacyAnimation,
            encoded,
            out var legacyDocument),
        "旧方式では保存済みカスタム軌跡を自動選択してはいけません。");
    Equal(0, legacyDocument.Strokes.Count, "旧方式の選択結果は空である必要があります。");

    True(
        WipePathSnapshot.TryResolveCustomDocument(
            WipePathInputMode.StrokeCollection,
            encoded,
            out var customDocument),
        "明示的に選択したカスタム軌跡を解決できません。");
    True(customDocument.Strokes.Count > 0, "有効なカスタム軌跡が空になりました。");

    True(
        !WipePathSnapshot.TryResolveCustomDocument(
            WipePathInputMode.StrokeCollection,
            "broken",
            out var invalidDocument),
        "破損カスタム軌跡を使用してはいけません。");
    Equal(
        0,
        invalidDocument.Strokes.Count,
        "破損データでは旧Animation経路へフォールバックできる空結果が必要です。");
}

static void VerifyLegacyAnimationPathAndMaskCompatibility()
{
    const int fps = 60;
    const int itemLength = 120;
    const int currentFrame = 83;

    var expectedSamples = Enumerable.Range(0, currentFrame + 1)
        .Select(frame => Sample(
            frame,
            frame / 100f,
            (100 - frame) / 100f,
            frame % 11 == 0 ? 0 : 1,
            0.3f,
            1,
            1,
            0))
        .ToArray();
    var actual = WipePathSnapshot.CreateLegacy(
        currentFrame,
        itemLength,
        fps,
        frame => new WipeLegacyAnimationValues(
            frame,
            100 - frame,
            frame % 11 == 0 ? 0 : 100,
            30,
            100,
            100,
            0));

    Equal(WipePathInputMode.LegacyAnimation, actual.InputMode, "旧Animation経路が選択される");
    True(actual.Style.IsDisabled, "旧Animationの既定スタイルは無効状態になる");
    True(expectedSamples.SequenceEqual(actual.Samples), "旧AnimationのPathサンプル列が従来値と一致する");

    var geometry = WipeMaskGeometry.Create(1920, 1080);
    var expectedStamps = WipeBrushStampGenerator.Generate(expectedSamples, 0, geometry);
    var actualStamps = WipeBrushStampGenerator.Generate(actual.Samples, 0, geometry, actual.Style);
    var probes = new[]
    {
        new Vector2(0.5f, 0.5f),
        new Vector2(0.65f, 0.5f),
        new Vector2(0.1f, 0.1f),
    };
    var expectedMask = ApplyStamps(new float[probes.Length], probes, expectedStamps);
    var actualMask = ApplyStamps(new float[probes.Length], probes, actualStamps);

    Equal(expectedMask.Length, actualMask.Length, "旧Animationの最終マスク比較点数");
    for (var i = 0; i < expectedMask.Length; i++)
    {
        NearlyEqual(expectedMask[i], actualMask[i], $"旧Animationの最終マスク[{i}]", 0.0001f);
    }
}

static void VerifySimpleEditingModeEnumsAndDefault()
{
    Equal(0, (int)GlassWipeEditingMode.Detailed, "詳細編集のenum値が不正です。");
    Equal(1, (int)GlassWipeEditingMode.Simple, "かんたん作成のenum値が不正です。");
    Equal("詳細設定", GetDisplayName(GlassWipeEditingMode.Detailed), "詳細設定の表示名が不正です。");
    Equal("標準設定", GetDisplayName(GlassWipeEditingMode.Simple), "標準設定の表示名が不正です。");

    Equal(5, Enum.GetValues<GlassWipeSimplePattern>().Length, "かんたん作成パターン数が不正です。");
    Equal(0, (int)GlassWipeSimplePattern.StraightOnce, "まっすぐ一回拭きのenum値が不正です。");
    Equal(1, (int)GlassWipeSimplePattern.GentleArcOnce, "ゆるい弧で一回拭きのenum値が不正です。");
    Equal(2, (int)GlassWipeSimplePattern.ShortRoundTrips, "きゅきゅっと短く往復のenum値が不正です。");
    Equal(3, (int)GlassWipeSimplePattern.ArcRoundTrips, "弧をなぞって往復のenum値が不正です。");
    Equal(4, (int)GlassWipeSimplePattern.OffsetRoundTrips, "少しずらしながら往復のenum値が不正です。");
    Equal("一回拭き（直線）", GetDisplayName(GlassWipeSimplePattern.StraightOnce), "一回拭き（直線）の表示名が不正です。");
    Equal("一回拭き（経由点あり）", GetDisplayName(GlassWipeSimplePattern.GentleArcOnce), "一回拭き（経由点あり）の表示名が不正です。");
    Equal("往復拭き（直線）", GetDisplayName(GlassWipeSimplePattern.ShortRoundTrips), "往復拭き（直線）の表示名が不正です。");
    Equal("往復拭き（経由点あり）", GetDisplayName(GlassWipeSimplePattern.ArcRoundTrips), "往復拭き（経由点あり）の表示名が不正です。");
    Equal("往復拭き（ずれ）", GetDisplayName(GlassWipeSimplePattern.OffsetRoundTrips), "往復拭き（ずれ）の表示名が不正です。");

    Equal(3, Enum.GetValues<GlassWipePathInterpolation>().Length, "補間方式数が不正です。");
    Equal(0, (int)GlassWipePathInterpolation.Linear, "直線補間のenum値が不正です。");
    Equal(1, (int)GlassWipePathInterpolation.Smooth, "なめらか補間のenum値が不正です。");
    Equal(2, (int)GlassWipePathInterpolation.CircularArc, "3点円弧補間のenum値が不正です。");
    Equal("直線", GetDisplayName(GlassWipePathInterpolation.Linear), "直線補間の表示名が不正です。");
    Equal("曲線（Catmull-Rom）", GetDisplayName(GlassWipePathInterpolation.Smooth), "Catmull-Rom補間の表示名が不正です。");
    Equal(
        "求心性のCatmull-Romスプラインで制御点を滑らかに結ぶ",
        GetDisplayDescription(GlassWipePathInterpolation.Smooth),
        "Catmull-Rom補間の説明文が不正です。");
    Equal("円弧（3点）", GetDisplayName(GlassWipePathInterpolation.CircularArc), "3点円弧補間の表示名が不正です。");

    Equal(
        GlassWipeEditingMode.Simple,
        GlassWipeEditingModeCompatibility.NewEffectDefault,
        "新規エフェクトの既定編集モードはかんたん作成である必要があります。");
    Equal(
        GlassWipePathInterpolation.CircularArc,
        GlassWipeDefaultSettings.SimpleInterpolation,
        "新規エフェクトの点のつなぎ方は円弧（3点）である必要があります。");
    NearlyEqual(
        10f,
        (float)GlassWipeDefaultSettings.BlurPixels,
        "新規エフェクトのぼかし量はテンプレートの10pxである必要があります。");
}

static void VerifySimpleEditingModeCompatibility()
{
    Equal(
        GlassWipeEditingMode.Detailed,
        GlassWipeEditingModeCompatibility.ResolveAfterDeserialization(
            editingModeWasSet: false,
            GlassWipeEditingModeCompatibility.NewEffectDefault),
        "EditingMode欠落の旧保存データは詳細編集として復元する必要があります。");
    Equal(
        GlassWipeEditingMode.Simple,
        GlassWipeEditingModeCompatibility.ResolveAfterDeserialization(
            editingModeWasSet: true,
            GlassWipeEditingMode.Simple),
        "明示的なかんたん作成を保持する必要があります。");
    Equal(
        GlassWipeEditingMode.Detailed,
        GlassWipeEditingModeCompatibility.ResolveAfterDeserialization(
            editingModeWasSet: true,
            GlassWipeEditingMode.Detailed),
        "明示的な詳細編集を保持する必要があります。");
    Equal(
        GlassWipeEditingMode.Detailed,
        GlassWipeEditingModeCompatibility.ResolveAfterDeserialization(
            editingModeWasSet: true,
            (GlassWipeEditingMode)99),
        "不正な編集モード値は詳細編集へフォールバックする必要があります。");
    Equal(
        GlassWipeEditingMode.Detailed,
        GlassWipeEditingModeCompatibility.Normalize((GlassWipeEditingMode)(-1)),
        "負の不正な編集モード値は詳細編集へフォールバックする必要があります。");
}

static void VerifyEditingModeVisibility()
{
    True(
        GlassWipeSimpleVisibility.IsSimpleMode(GlassWipeEditingMode.Simple),
        "かんたん作成ではかんたん設定を表示する必要があります。");
    True(
        !GlassWipeSimpleVisibility.IsDetailedMode(GlassWipeEditingMode.Simple),
        "かんたん作成では詳細設定を表示してはいけません。");
    True(
        !GlassWipeSimpleVisibility.IsSimpleMode(GlassWipeEditingMode.Detailed),
        "詳細編集ではかんたん設定を表示してはいけません。");
    True(
        GlassWipeSimpleVisibility.IsDetailedMode(GlassWipeEditingMode.Detailed),
        "詳細編集では軌跡プリセットを含む詳細設定を表示する必要があります。");
    True(
        !GlassWipeSimpleVisibility.IsSimpleMode((GlassWipeEditingMode)99) &&
        !GlassWipeSimpleVisibility.IsDetailedMode((GlassWipeEditingMode)99),
        "不正な編集モードでは片方の設定を誤表示してはいけません。");
    True(
        GlassWipeSimpleVisibility.AreSimpleGeneratedBrushSettingsVisible(
            GlassWipeEditingMode.Simple,
            WipePathInputMode.LegacyAnimation),
        "かんたん作成では保存済みの軌跡種別にかかわらずブラシ設定を表示する必要があります。");
    True(
        GlassWipeSimpleVisibility.AreSimpleGeneratedBrushSettingsVisible(
            GlassWipeEditingMode.Detailed,
            WipePathInputMode.SimpleGenerated),
        "詳細編集のかんたん作成軌跡ではブラシ設定を表示する必要があります。");
    True(
        !GlassWipeSimpleVisibility.AreSimpleGeneratedBrushSettingsVisible(
            GlassWipeEditingMode.Detailed,
            WipePathInputMode.StrokeCollection),
        "詳細編集のカスタム軌跡ではかんたん作成用ブラシ倍率を表示してはいけません。");
    True(
        GlassWipeSimpleVisibility.AreSimpleGeneratedPathSettingsVisible(
            GlassWipeEditingMode.Simple,
            WipePathInputMode.LegacyAnimation) &&
        GlassWipeSimpleVisibility.AreSimpleGeneratedPathSettingsVisible(
            GlassWipeEditingMode.Detailed,
            WipePathInputMode.SimpleGenerated) &&
        !GlassWipeSimpleVisibility.AreSimpleGeneratedPathSettingsVisible(
            GlassWipeEditingMode.Detailed,
            WipePathInputMode.LegacyAnimation) &&
        !GlassWipeSimpleVisibility.AreSimpleGeneratedPathSettingsVisible(
            GlassWipeEditingMode.Detailed,
            WipePathInputMode.StrokeCollection),
        "定型軌跡パラメータの表示条件が軌跡種類と一致しません。");

    var effect = new GlassWipeVideoEffect
    {
        EditingMode = GlassWipeEditingMode.Detailed,
        PathInputMode = WipePathInputMode.StrokeCollection,
        BrushShape = GlassWipeBrushShape.Circle,
    };
    var changedProperties = new HashSet<string>(StringComparer.Ordinal);
    effect.PropertyChanged += (_, eventArgs) =>
    {
        if (!string.IsNullOrEmpty(eventArgs.PropertyName))
        {
            changedProperties.Add(eventArgs.PropertyName);
        }
    };
    effect.PathInputMode = WipePathInputMode.SimpleGenerated;
    foreach (var propertyName in new[]
             {
                 nameof(GlassWipeVideoEffect.AreDetailedPathSettingsVisible),
                 nameof(GlassWipeVideoEffect.AreSimpleGeneratedPathSettingsVisible),
                 nameof(GlassWipeVideoEffect.AreSimpleGeneratedBrushSettingsVisible),
                 nameof(GlassWipeVideoEffect.IsDetailedCustomPathVisible),
                 nameof(GlassWipeVideoEffect.IsDetailedLegacyPathVisible),
             })
    {
        True(
            changedProperties.Contains(propertyName),
            $"軌跡種類の変更時に{propertyName}の表示更新を通知する必要があります。");
    }

    changedProperties.Clear();
    effect.BrushShape = GlassWipeBrushShape.UserImage;
    foreach (var propertyName in new[]
             {
                 nameof(GlassWipeVideoEffect.IsUserBrushVisible),
                 nameof(GlassWipeVideoEffect.IsPngBrushSizeVisible),
                 nameof(GlassWipeVideoEffect.IsCircleBrushSizeVisible),
                 nameof(GlassWipeVideoEffect.IsEllipseBrushSizeVisible),
                 nameof(GlassWipeVideoEffect.IsRectangleBrushSizeVisible),
             })
    {
        True(
            changedProperties.Contains(propertyName),
            $"ブラシ形状の変更時に{propertyName}の表示更新を通知する必要があります。");
    }

    var effectSource = File.ReadAllText(
        Path.Combine(
            FindRepositoryRoot(),
            "src",
            "YMM4GlassWipe",
            "GlassWipeVideoEffect.cs"));
    True(
        effectSource.Contains(
            "[Display(GroupName = \"設定\", Name = \"設定方法\", Description = \"標準設定または詳細設定を選択\")]",
            StringComparison.Ordinal),
        "編集モード項目は設定方法と表示する必要があります。");
    True(
        effectSource.Contains(
            "Name = \"描画品質\", Description = \"軌跡を構成するブラシの間隔を選択します。\\n軽量は間隔を広げ、標準は通常の間隔、高品質は間隔を狭めます。\\n自動は通常の定型軌跡で標準、往復系で高品質を使用します。\\n形状や拭き取り強度など、ほかのブラシ設定は変更しません。\\n高品質は、4Kや長尺の素材で描画負荷が増える場合があります。\\n動作が重い場合は、自動、標準、または軽量を選択してください。\"",
            StringComparison.Ordinal),
        "定型軌跡品質のツールチップ全文が不正です。");
    True(
        effectSource.Contains(
            "Name = \"描画品質\", Description = \"軌跡を構成するブラシの間隔を選択します。\\n軽量は間隔を広げ、標準は通常の間隔、高品質は間隔を狭めます。\\n形状や拭き取り強度など、ほかのブラシ設定は変更しません。\\n高品質は、4Kや長尺の素材で描画負荷が増える場合があります。\\n動作が重い場合は、標準、または軽量を選択してください。\"",
            StringComparison.Ordinal),
        "詳細軌跡品質の説明には自動規則を含めず、負荷の注意を表示する必要があります。");
    True(
        effectSource.Contains(
            "private GlassWipeSimpleGeneratedQuality _simpleGeneratedQuality =\r\n        GlassWipeSimpleGeneratedQuality.Auto;",
            StringComparison.Ordinal) ||
        effectSource.Contains(
            "private GlassWipeSimpleGeneratedQuality _simpleGeneratedQuality =\n        GlassWipeSimpleGeneratedQuality.Auto;",
            StringComparison.Ordinal),
        "新規エフェクトの定型軌跡品質は自動である必要があります。");
    True(
        effectSource.Contains(
            "GlassWipeSimpleGeneratedQualityCompatibility.Normalize(value)",
            StringComparison.Ordinal) &&
        effectSource.Contains(
            "_simpleGeneratedQuality = GlassWipeSimpleGeneratedQualityCompatibility.Normalize(",
            StringComparison.Ordinal),
        "定型軌跡品質は設定時と逆シリアル化後に補正する必要があります。");
    True(
        HasPropertyCustomAttribute(
            typeof(GlassWipeSimpleGeneratedQuality).Assembly.Location,
            "YMM4GlassWipe",
            nameof(GlassWipeVideoEffect),
            nameof(GlassWipeVideoEffect.SimpleGeneratedQuality),
            "YukkuriMovieMaker.ItemEditor.CustomVisibilityAttributes",
            "ShowPropertyEditorWhenAttribute"),
        "定型軌跡品質には軌跡方式別の可視条件が必要です。");
    foreach (var propertyName in new[]
             {
                 nameof(GlassWipeVideoEffect.SimplePattern),
                 nameof(GlassWipeVideoEffect.SimpleProgress),
                 nameof(GlassWipeVideoEffect.SimpleStartX),
                 nameof(GlassWipeVideoEffect.SimpleStartY),
                 nameof(GlassWipeVideoEffect.SimpleEndX),
                 nameof(GlassWipeVideoEffect.SimpleEndY),
             })
    {
        var attribute = typeof(GlassWipeVideoEffect)
            .GetProperty(propertyName)!
            .GetCustomAttributesData()
            .Single(data => data.AttributeType.Name == "ShowPropertyEditorWhenAttribute");
        Equal(
            nameof(GlassWipeVideoEffect.AreSimpleGeneratedPathSettingsVisible),
            attribute.ConstructorArguments[0].Value as string,
            propertyName + "の定型軌跡表示条件");
    }
    foreach (var propertyName in new[]
             {
                 nameof(GlassWipeVideoEffect.BrushX),
                 nameof(GlassWipeVideoEffect.BrushY),
                 nameof(GlassWipeVideoEffect.Contact),
             })
    {
        var display = typeof(GlassWipeVideoEffect)
            .GetProperty(propertyName)!
            .GetCustomAttribute<DisplayAttribute>();
        Equal("軌跡", display?.GroupName, propertyName + "の表示グループ");
    }

    Equal(
        "直径",
        GlassWipeBrushUiText.CircleDiameterName,
        "円ブラシの直径は短い表示名を使う必要があります。");
    Equal(
        "幅",
        GlassWipeBrushUiText.WidthName,
        "楕円と四角形の横寸法を幅として表示する必要があります。");
    Equal(
        "高さ",
        GlassWipeBrushUiText.HeightName,
        "楕円と四角形の縦寸法を高さとして表示する必要があります。");
    True(
        GlassWipeBrushUiText.CircleDiameterDescription.Contains("ピクセル", StringComparison.Ordinal) &&
        GlassWipeBrushUiText.EllipseWidthDescription.Contains("楕円", StringComparison.Ordinal) &&
        GlassWipeBrushUiText.EllipseHeightDescription.Contains("ピクセル", StringComparison.Ordinal) &&
        GlassWipeBrushUiText.RectangleWidthDescription.Contains("四角形", StringComparison.Ordinal) &&
        GlassWipeBrushUiText.RectangleHeightDescription.Contains("ピクセル", StringComparison.Ordinal),
        "生成形状ごとの直径・幅・高さを入力素材上のピクセル指定として説明する必要があります。");
}

static void VerifyDetailedPathInputModeCompatibility()
{
    Equal(0, (int)WipePathInputMode.LegacyAnimation, "旧キーフレームのenum値が不正です。");
    Equal(1, (int)WipePathInputMode.StrokeCollection, "カスタム軌跡のenum値が不正です。");
    Equal(2, (int)WipePathInputMode.SimpleGenerated, "かんたん作成軌跡のenum値が不正です。");
    Equal("定型軌跡", GetDisplayName(WipePathInputMode.SimpleGenerated), "定型軌跡の表示名が不正です。");
    Equal(
        WipePathInputMode.SimpleGenerated,
        WipePathInputModeCompatibility.NewEffectDefault,
        "新規エフェクトの軌跡種別はかんたん作成と同じ軌跡である必要があります。");

    Equal(
        WipePathInputMode.SimpleGenerated,
        WipePathInputModeCompatibility.ResolveAfterDeserialization(
            pathInputModeWasSet: false,
            WipePathInputModeCompatibility.NewEffectDefault,
            GlassWipeEditingMode.Simple),
        "旧保存データをかんたん作成で開く場合は同じ軌跡を使用する必要があります。");
    Equal(
        WipePathInputMode.LegacyAnimation,
        WipePathInputModeCompatibility.ResolveAfterDeserialization(
            pathInputModeWasSet: false,
            WipePathInputModeCompatibility.NewEffectDefault,
            GlassWipeEditingMode.Detailed),
        "旧保存データを詳細編集で開く場合は従来キーフレームを維持する必要があります。");

    foreach (var mode in Enum.GetValues<WipePathInputMode>())
    {
        Equal(
            mode,
            WipePathInputModeCompatibility.ResolveAfterDeserialization(
                pathInputModeWasSet: true,
                mode,
                GlassWipeEditingMode.Detailed),
            $"明示された軌跡種別 {mode} を保持する必要があります。");
    }

    Equal(
        WipePathInputMode.LegacyAnimation,
        WipePathInputModeCompatibility.Normalize((WipePathInputMode)99),
        "不正な軌跡種別は従来キーフレームへフォールバックする必要があります。");
    True(
        WipePathInputModeCompatibility.UsesSimpleGenerator(
            GlassWipeEditingMode.Simple,
            WipePathInputMode.LegacyAnimation),
        "かんたん作成では軌跡種別にかかわらずかんたん生成器を使用する必要があります。");
    True(
        WipePathInputModeCompatibility.UsesSimpleGenerator(
            GlassWipeEditingMode.Detailed,
            WipePathInputMode.SimpleGenerated),
        "詳細モードへ切り替えた直後もかんたん作成と同じ軌跡を使用する必要があります。");
    True(
        !WipePathInputModeCompatibility.UsesSimpleGenerator(
            GlassWipeEditingMode.Detailed,
            WipePathInputMode.StrokeCollection),
        "カスタム軌跡選択時はかんたん生成器を使用してはいけません。");

    var generatedStyle = WipeSimplePathGenerator.FixedProgressiveStyle;
    Equal(
        generatedStyle with
        {
            BrushShape = GlassWipeBrushShape.ShoePrint,
            RotationFollow = 0.4f,
            BrushMirror = true,
        },
        WipePathSnapshot.ResolveSimpleGeneratedStyle(
            GlassWipeEditingMode.Simple,
            generatedStyle,
            GlassWipeBrushShape.ShoePrint,
            40,
            true),
        "かんたん作成では選択したブラシ形状・回転追従・左右反転を反映する必要があります。");
    var detailedStyle = WipePathSnapshot.ResolveSimpleGeneratedStyle(
        GlassWipeEditingMode.Detailed,
        generatedStyle,
        GlassWipeBrushShape.ShoePrint);
    Equal(
        generatedStyle with { BrushShape = GlassWipeBrushShape.ShoePrint },
        detailedStyle,
        "詳細編集のかんたん作成軌跡では形状だけを固定描画スタイルへ反映する必要があります。");
    var detailedSnapshot = WipeSimplePathGenerator.Generate(
            GlassWipeSimplePattern.ShortRoundTrips,
            GlassWipePathInterpolation.Linear,
            new Vector2(10, 50),
            new Vector2(50, 50),
            new Vector2(90, 50),
            2,
            60,
            120)
        .CreateSnapshot(60, detailedStyle);
    Equal(
        GlassWipeBrushShape.ShoePrint,
        detailedSnapshot.Style.BrushShape,
        "詳細編集のかんたん作成軌跡へブラシ形状を渡す必要があります。");
    Equal(
        generatedStyle.AccumulationMode,
        detailedSnapshot.Style.AccumulationMode,
        "形状適用後も往復軌跡の累積方式を維持する必要があります。");

    var generatedSamples = detailedSnapshot.Samples;
    var nativePngSamples = WipePathSnapshot.ResolveSimpleGeneratedSamples(
        GlassWipeEditingMode.Detailed,
        generatedSamples,
        _ => new WipeBrushPixelDimensions(230, 230),
        _ => 0);
    NearlyEqual(
        230,
        nativePngSamples[0].BrushWidthPixels ?? -1,
        "PNGブラシの元キャンバス幅を使用する必要があります。",
        0.0001f);
    NearlyEqual(
        230,
        nativePngSamples[0].BrushHeightPixels ?? -1,
        "PNGブラシの元キャンバス高さを使用する必要があります。",
        0.0001f);

    var circleSamples = WipePathSnapshot.ResolveSimpleGeneratedSamples(
        GlassWipeEditingMode.Simple,
        generatedSamples,
        _ => new WipeBrushPixelDimensions(50, 50),
        _ => 0);
    NearlyEqual(50, circleSamples[0].BrushWidthPixels ?? -1, "円の直径", 0.0001f);
    NearlyEqual(50, circleSamples[0].BrushHeightPixels ?? -1, "円の直径", 0.0001f);

    var ellipseSamples = WipePathSnapshot.ResolveSimpleGeneratedSamples(
        GlassWipeEditingMode.Detailed,
        generatedSamples,
        _ => new WipeBrushPixelDimensions(25, 50),
        _ => 0);
    NearlyEqual(25, ellipseSamples[0].BrushWidthPixels ?? -1, "楕円の幅", 0.0001f);
    NearlyEqual(50, ellipseSamples[0].BrushHeightPixels ?? -1, "楕円の高さ", 0.0001f);

    var adjustedSamples = WipePathSnapshot.ResolveSimpleGeneratedSamples(
        GlassWipeEditingMode.Simple,
        generatedSamples,
        _ => new WipeBrushPixelDimensions(100, 30),
        _ => 90);
    NearlyEqual(100, adjustedSamples[0].BrushWidthPixels ?? -1, "四角形の幅", 0.0001f);
    NearlyEqual(30, adjustedSamples[0].BrushHeightPixels ?? -1, "四角形の高さ", 0.0001f);
    NearlyEqual(
        generatedSamples[0].RotationRadians + MathF.PI / 2,
        adjustedSamples[0].RotationRadians,
        "かんたん作成の回転角",
        0.0001f);
    NearlyEqual(
        0.75f,
        WipePathSnapshot.ResolveSimpleGeneratedStyle(
            GlassWipeEditingMode.Detailed,
            generatedStyle,
            GlassWipeBrushShape.Hand,
            75).RotationFollow,
        "かんたん作成の回転追従",
        0.0001f);
}

static void VerifyDetailedBuiltInPresets()
{
    Equal(3, Enum.GetValues<DetailedWipeBuiltInPreset>().Length, "内蔵プリセット数が不正です。");
    Equal("ハート", DetailedWipePresetFactory.GetDisplayName(DetailedWipeBuiltInPreset.Heart), "ハートの表示名");
    Equal("相合い傘", DetailedWipePresetFactory.GetDisplayName(DetailedWipeBuiltInPreset.LoveUmbrella), "相合い傘の表示名");
    Equal("スマイル", DetailedWipePresetFactory.GetDisplayName(DetailedWipeBuiltInPreset.Smiley), "スマイルの表示名");

    foreach (var preset in Enum.GetValues<DetailedWipeBuiltInPreset>())
    {
        var state = DetailedWipePresetFactory.Create(preset);
        True(state.TrySanitize(out var sanitized), $"{preset} の状態を補正できる必要があります。");
        True(
            WipeStrokeDocumentCodec.TryDecode(sanitized.CustomPathData, out var document),
            $"{preset} の軌跡を復元できる必要があります。");
        True(document.Strokes.Count > 0, $"{preset} は1本以上のストロークを持つ必要があります。");

        foreach (var stroke in document.Strokes)
        {
            True(stroke.Points.Count > 0, $"{preset} に空ストロークがあってはいけません。");
            for (var index = 0; index < stroke.Points.Count; index++)
            {
                var point = stroke.Points[index];
                True(point.IsFinite, $"{preset} の点は有限値である必要があります。");
                True(
                    point.TimelinePercent is >= 0 and <= 100 &&
                    point.X is >= 0 and <= WipeStrokeDocument.CanvasPixelWidth &&
                    point.Y is >= 0 and <= WipeStrokeDocument.CanvasPixelHeight &&
                    point.Contact is >= 0 and <= 100,
                    $"{preset} の点は有効範囲内である必要があります。");
                if (index > 0)
                {
                    True(
                        point.TimelinePercent > stroke.Points[index - 1].TimelinePercent,
                        $"{preset} の同一ストローク内時刻は単調増加である必要があります。");
                }
            }
        }

        var samples = WipeStrokeSampler.Expand(document, 300, 300);
        True(samples.Length > 0, $"{preset} は最終フレームまでサンプルを生成する必要があります。");
        True(
            samples.All(sample =>
                float.IsFinite(sample.X) &&
                float.IsFinite(sample.Y) &&
                float.IsFinite(sample.Contact) &&
                sample.X is >= 0 and <= 1 &&
                sample.Y is >= 0 and <= 1 &&
                sample.Contact is >= 0 and <= 1),
            $"{preset} のサンプルは有限かつ正規化範囲内である必要があります。");
    }

    True(
        WipeStrokeDocumentCodec.TryDecode(
            DetailedWipePresetFactory.Create(DetailedWipeBuiltInPreset.Heart).CustomPathData,
            out var heart),
        "ハートを復元できる必要があります。");
    Equal(1, heart.Strokes.Count, "ハートは一筆の軌跡である必要があります。");
    NearlyEqual(
        (float)heart.Strokes[0].Points[0].X,
        (float)heart.Strokes[0].Points[^1].X,
        "ハートの始点と終点X",
        0.001f);
    NearlyEqual(
        (float)heart.Strokes[0].Points[0].Y,
        (float)heart.Strokes[0].Points[^1].Y,
        "ハートの始点と終点Y",
        0.001f);

    True(
        WipeStrokeDocumentCodec.TryDecode(
            DetailedWipePresetFactory.Create(DetailedWipeBuiltInPreset.LoveUmbrella).CustomPathData,
            out var umbrella),
        "相合い傘を復元できる必要があります。");
    Equal(1, umbrella.Strokes.Count, "相合い傘は一筆の軌跡である必要があります。");
    var umbrellaPoints = umbrella.Strokes[0].Points;
    Equal(10, umbrellaPoints.Count, "相合い傘の制御点数が不正です。");
    NearlyEqual(960, (float)umbrellaPoints[0].X, "相合い傘の頂点X", 0.001f);
    NearlyEqual(216, (float)umbrellaPoints[0].Y, "相合い傘の頂点Y", 0.001f);
    True(
        umbrellaPoints[2].X < umbrellaPoints[0].X &&
        umbrellaPoints[6].X > umbrellaPoints[0].X,
        "相合い傘は頂点から左辺、下辺、右辺の順に描く必要があります。");
    NearlyEqual(
        (float)umbrellaPoints[0].X,
        (float)umbrellaPoints[^2].X,
        "相合い傘は持ち手を描く前に頂点へ戻る必要があります。",
        0.001f);
    NearlyEqual(
        (float)umbrellaPoints[0].Y,
        (float)umbrellaPoints[^2].Y,
        "相合い傘は持ち手を描く前に頂点へ戻る必要があります。",
        0.001f);
    True(
        umbrellaPoints[^1].Y > umbrellaPoints[^2].Y,
        "相合い傘は最後に頂点から持ち手下端へ描く必要があります。");

    True(
        WipeStrokeDocumentCodec.TryDecode(
            DetailedWipePresetFactory.Create(DetailedWipeBuiltInPreset.Smiley).CustomPathData,
            out var smiley),
        "にこちゃんを復元できる必要があります。");
    True(smiley.Strokes.Count >= 4, "にこちゃんは輪郭・両目・口を別ストロークで持つ必要があります。");
}

static void VerifyDetailedPresetCodec()
{
    var state = DetailedWipePresetFactory.Create(DetailedWipeBuiltInPreset.Heart);
    state.BrushShape = GlassWipeBrushShape.ShoePrint;
    state.BrushMirror = true;
    state.CircleDiameterPixels = new DetailedWipeAnimationState
    {
        From = double.NaN,
        To = 9000,
        AnimationType = 7,
    };
    state.EllipseWidthPixels = new DetailedWipeAnimationState
    {
        From = -10,
        To = 50,
        AnimationType = 4,
    };
    state.EllipseHeightPixels = DetailedWipePresetFactory.ConstantAnimation(75);
    state.RectangleWidthPixels = DetailedWipePresetFactory.ConstantAnimation(100);
    state.RectangleHeightPixels = DetailedWipePresetFactory.ConstantAnimation(30);
    state.PngBrushSizeScale = new DetailedWipeAnimationState
    {
        From = double.NaN,
        To = 500,
        AnimationType = 6,
    };
    state.PathSmoothing = -20;
    state.PathJitter = double.PositiveInfinity;
    state.BrushSoftness = 250;
    state.SimpleGeneratedBrushRotation =
        DetailedWipePresetFactory.ConstantAnimation(-45);
    state.SimpleGeneratedBrushRotationFollow = 80;
    state.Quality = GlassWipeQuality.High;
    state.SimpleGeneratedQuality = GlassWipeSimpleGeneratedQuality.Low;

    var encoded = DetailedWipePresetCodec.Encode(state);
    True(DetailedWipePresetCodec.TryDecode(encoded, out var decoded), "プリセットを往復保存できる必要があります。");
    NearlyEqual(50, (float)decoded.CircleDiameterPixels.From, "非有限円直径の補正", 0.001f);
    NearlyEqual(8192, (float)decoded.CircleDiameterPixels.To, "円直径上限の補正", 0.001f);
    Equal(7, decoded.CircleDiameterPixels.AnimationType, "円直径のアニメーション種別を保持する必要があります。");
    NearlyEqual(0, (float)decoded.EllipseWidthPixels.From, "楕円幅下限の補正", 0.001f);
    NearlyEqual(50, (float)decoded.EllipseWidthPixels.To, "楕円幅を保持する必要があります。", 0.001f);
    Equal(4, decoded.EllipseWidthPixels.AnimationType, "楕円幅のアニメーション種別を保持する必要があります。");
    NearlyEqual(75, (float)decoded.EllipseHeightPixels.From, "楕円高さ", 0.001f);
    NearlyEqual(100, (float)decoded.RectangleWidthPixels.From, "四角形幅", 0.001f);
    NearlyEqual(30, (float)decoded.RectangleHeightPixels.From, "四角形高さ", 0.001f);
    NearlyEqual(100, (float)decoded.PngBrushSizeScale.From, "非有限PNG倍率の補正", 0.001f);
    NearlyEqual(400, (float)decoded.PngBrushSizeScale.To, "PNG倍率上限の補正", 0.001f);
    Equal(6, decoded.PngBrushSizeScale.AnimationType, "PNG倍率のアニメーション種別を保持する必要があります。");
    NearlyEqual(0, (float)decoded.PathSmoothing, "平滑化下限の補正", 0.001f);
    NearlyEqual(0, (float)decoded.PathJitter, "非有限揺れの補正", 0.001f);
    NearlyEqual(100, (float)decoded.BrushSoftness, "柔らかさ上限の補正", 0.001f);
    Equal(
        GlassWipeBrushShape.ShoePrint,
        decoded.BrushShape,
        "ブラシ形状をプリセットへ保存する必要があります。");
    True(decoded.BrushMirror, "左右反転をプリセットへ保存する必要があります。");
    NearlyEqual(
        -45,
        (float)decoded.SimpleGeneratedBrushRotation.From,
        "かんたん作成の回転角",
        0.001f);
    NearlyEqual(
        80,
        (float)decoded.SimpleGeneratedBrushRotationFollow,
        "かんたん作成の回転追従",
        0.001f);
    Equal<GlassWipeQuality?>(
        GlassWipeQuality.High,
        decoded.Quality,
        "詳細軌跡品質をプリセットへ保存する必要があります。");
    Equal<GlassWipeSimpleGeneratedQuality?>(
        GlassWipeSimpleGeneratedQuality.Low,
        decoded.SimpleGeneratedQuality,
        "定型軌跡品質をプリセットへ保存する必要があります。");

    var version2Encoded = encoded
        .Replace(
            $"\"dataSchemaVersion\":{DetailedWipePresetState.CurrentVersion}",
            "\"dataSchemaVersion\":2",
            StringComparison.Ordinal)
        .Replace(
            ",\"brushMirror\":true",
            string.Empty,
            StringComparison.Ordinal)
        .Replace(
            ",\"userBrushId\":\"00000000-0000-0000-0000-000000000000\"",
            string.Empty,
            StringComparison.Ordinal)
        .Replace(
            $",\"quality\":{(int)GlassWipeQuality.High}",
            string.Empty,
            StringComparison.Ordinal)
        .Replace(
            $",\"simpleGeneratedQuality\":{(int)GlassWipeSimpleGeneratedQuality.Low}",
            string.Empty,
            StringComparison.Ordinal);
    True(
        DetailedWipePresetCodec.TryDecode(version2Encoded, out var version2Decoded),
        "v2プリセットを読み込める必要があります。");
    True(
        !version2Decoded.BrushMirror,
        "左右反転がないv2プリセットは反転なしとして読む必要があります。");
    Equal(
        Guid.Empty,
        version2Decoded.UserBrushId,
        "ユーザーブラシIDがないv2プリセットは空IDとして読む必要があります。");
    Equal<GlassWipeQuality?>(
        GlassWipeQuality.Standard,
        version2Decoded.Quality,
        "詳細軌跡品質がない旧プリセットは標準として読む必要があります。");
    Equal<GlassWipeSimpleGeneratedQuality?>(
        GlassWipeSimpleGeneratedQuality.Auto,
        version2Decoded.SimpleGeneratedQuality,
        "定型軌跡品質がない旧プリセットは自動として読む必要があります。");

    var legacyEncoded = encoded
        .Replace(
            $"\"dataSchemaVersion\":{DetailedWipePresetState.CurrentVersion}",
            "\"dataSchemaVersion\":1",
            StringComparison.Ordinal)
        .Replace(
            $",\"brushShape\":{(int)GlassWipeBrushShape.ShoePrint}",
            string.Empty,
            StringComparison.Ordinal)
        .Replace(
            ",\"brushMirror\":true",
            string.Empty,
            StringComparison.Ordinal)
        .Replace(
            ",\"userBrushId\":\"00000000-0000-0000-0000-000000000000\"",
            string.Empty,
            StringComparison.Ordinal);
    True(
        DetailedWipePresetCodec.TryDecode(legacyEncoded, out var legacyDecoded),
        "v1プリセットを読み込める必要があります。");
    Equal(
        DetailedWipePresetState.CurrentVersion,
        legacyDecoded.DataSchemaVersion,
        "v1プリセットを現行版へ正規化する必要があります。");
    Equal(
        GlassWipeBrushShape.Circle,
        legacyDecoded.BrushShape,
        "形状がないv1プリセットは円として読む必要があります。");
    True(
        !legacyDecoded.BrushMirror,
        "左右反転がないv1プリセットは反転なしとして読む必要があります。");

    var version14State = DetailedWipePresetFactory.Create(DetailedWipeBuiltInPreset.Heart);
    version14State.DataSchemaVersion =
        DetailedWipePresetState.PngBrushSizeScaleIntroducedVersion - 1;
    version14State.PngBrushSizeScale = new DetailedWipeAnimationState
    {
        From = 250,
        To = 300,
        AnimationType = 4,
    };
    True(
        version14State.TrySanitize(out var version14Decoded),
        "PNG倍率導入前の内部プリセットを補正できる必要があります。");
    NearlyEqual(
        100f,
        (float)version14Decoded.PngBrushSizeScale.From,
        "PNG倍率導入前の内部プリセットは100%へ補正する必要があります。",
        0.001f);
    NearlyEqual(
        100f,
        (float)version14Decoded.PngBrushSizeScale.To,
        "PNG倍率導入前の内部プリセット終点は100%へ補正する必要があります。",
        0.001f);

    var invalidShapeEncoded = encoded.Replace(
        $"\"brushShape\":{(int)GlassWipeBrushShape.ShoePrint}",
        "\"brushShape\":99",
        StringComparison.Ordinal);
    True(
        DetailedWipePresetCodec.TryDecode(
            invalidShapeEncoded,
            out var invalidShapeDecoded),
        "不正な形状値を含むプリセットを安全に補正する必要があります。");
    Equal(
        GlassWipeBrushShape.Circle,
        invalidShapeDecoded.BrushShape,
        "不正な形状値は円へ補正する必要があります。");

    var invalidQualityEncoded = encoded
        .Replace(
            $"\"quality\":{(int)GlassWipeQuality.High}",
            "\"quality\":99",
            StringComparison.Ordinal)
        .Replace(
            $"\"simpleGeneratedQuality\":{(int)GlassWipeSimpleGeneratedQuality.Low}",
            "\"simpleGeneratedQuality\":99",
            StringComparison.Ordinal);
    True(
        DetailedWipePresetCodec.TryDecode(
            invalidQualityEncoded,
            out var invalidQualityDecoded),
        "不正な品質値を含むプリセットを安全に補正する必要があります。");
    Equal<GlassWipeQuality?>(
        GlassWipeQuality.Standard,
        invalidQualityDecoded.Quality,
        "不正な詳細軌跡品質は標準へ補正する必要があります。");
    Equal<GlassWipeSimpleGeneratedQuality?>(
        GlassWipeSimpleGeneratedQuality.Auto,
        invalidQualityDecoded.SimpleGeneratedQuality,
        "不正な定型軌跡品質は自動へ補正する必要があります。");

    True(!DetailedWipePresetCodec.TryDecode(null, out _), "nullプリセットを拒否する必要があります。");
    True(!DetailedWipePresetCodec.TryDecode("{}", out _), "必須軌跡のないプリセットを拒否する必要があります。");
    True(
        !DetailedWipePresetCodec.TryDecode(
            new string('x', DetailedWipePresetCodec.MaximumEncodedLength + 1),
            out _),
        "上限超過プリセットを拒否する必要があります。");

    state.DataSchemaVersion = DetailedWipePresetState.CurrentVersion + 1;
    True(!state.TrySanitize(out _), "未知のプリセット版を拒否する必要があります。");
}

static void VerifyDetailedPresetScopes()
{
    Equal(0, (int)DetailedWipePresetScope.All, "旧プリセット互換の全設定値");
    Equal(1, (int)DetailedWipePresetScope.Path, "軌跡の値");
    Equal(2, (int)DetailedWipePresetScope.Brush, "ブラシの値");
    Equal("軌跡", DetailedWipePresetScopePolicy.GetDisplayName(DetailedWipePresetScope.Path), "軌跡表示名");
    Equal("ブラシ", DetailedWipePresetScopePolicy.GetDisplayName(DetailedWipePresetScope.Brush), "ブラシ表示名");
    Equal("全設定", DetailedWipePresetScopePolicy.GetDisplayName(DetailedWipePresetScope.All), "全設定表示名");
    True(
        !DetailedWipePresetScopePolicy.IsValid((DetailedWipePresetScope)99),
        "未知のプリセット種類を拒否する必要があります。");

    var current = DetailedWipePresetFactory.Create(DetailedWipeBuiltInPreset.Heart);
    current.BrushShape = GlassWipeBrushShape.Ellipse;
    current.BrushMirror = false;
    current.CircleDiameterPixels = DetailedWipePresetFactory.ConstantAnimation(23);
    current.EllipseWidthPixels = DetailedWipePresetFactory.ConstantAnimation(145);
    current.PngBrushSizeScale = DetailedWipePresetFactory.ConstantAnimation(125);
    current.PathSmoothing = 11;
    current.PathJitter = 1.25;
    current.JitterSeed = 27;
    current.Quality = GlassWipeQuality.Low;
    current.SimpleGeneratedQuality = GlassWipeSimpleGeneratedQuality.High;

    var preset = DetailedWipePresetFactory.Create(DetailedWipeBuiltInPreset.Smiley);
    preset.BrushShape = GlassWipeBrushShape.Hand;
    preset.BrushMirror = true;
    preset.CircleDiameterPixels = DetailedWipePresetFactory.ConstantAnimation(67);
    preset.EllipseWidthPixels = DetailedWipePresetFactory.ConstantAnimation(225);
    preset.PngBrushSizeScale = DetailedWipePresetFactory.ConstantAnimation(250);
    preset.PathSmoothing = 88;
    preset.PathJitter = 3.5;
    preset.JitterSeed = 91;
    preset.Quality = GlassWipeQuality.High;
    preset.SimpleGeneratedQuality = GlassWipeSimpleGeneratedQuality.Low;

    True(
        DetailedWipePresetStateMerger.TryMerge(
            current,
            preset,
            DetailedWipePresetScope.Path,
            out var pathOnly),
        "軌跡だけを統合できる必要があります。");
    Equal(preset.CustomPathData, pathOnly.CustomPathData, "軌跡だけのカスタム軌跡");
    NearlyEqual(88, (float)pathOnly.PathSmoothing, "軌跡だけの平滑化", 0.001f);
    Equal(current.BrushShape, pathOnly.BrushShape, "軌跡だけではブラシ形状を保持する必要があります。");
    NearlyEqual(23, (float)pathOnly.CircleDiameterPixels.From, "軌跡だけの円直径保持", 0.001f);
    NearlyEqual(
        145,
        (float)pathOnly.EllipseWidthPixels.From,
        "軌跡だけの楕円幅保持",
        0.001f);
    NearlyEqual(
        125,
        (float)pathOnly.PngBrushSizeScale.From,
        "軌跡だけのPNG倍率保持",
        0.001f);
    Equal<GlassWipeQuality?>(current.Quality, pathOnly.Quality, "軌跡だけの詳細品質保持");
    Equal<GlassWipeSimpleGeneratedQuality?>(
        current.SimpleGeneratedQuality,
        pathOnly.SimpleGeneratedQuality,
        "軌跡だけの定型軌跡品質保持");

    True(
        DetailedWipePresetStateMerger.TryMerge(
            current,
            preset,
            DetailedWipePresetScope.Brush,
            out var brushOnly),
        "ブラシだけを統合できる必要があります。");
    Equal(current.CustomPathData, brushOnly.CustomPathData, "ブラシだけではカスタム軌跡を保持する必要があります。");
    NearlyEqual(11, (float)brushOnly.PathSmoothing, "ブラシだけの平滑化保持", 0.001f);
    NearlyEqual(1.25f, (float)brushOnly.PathJitter, "ブラシだけの微小揺れ保持", 0.001f);
    NearlyEqual(27, (float)brushOnly.JitterSeed, "ブラシだけのSeed保持", 0.001f);
    Equal(GlassWipeBrushShape.Hand, brushOnly.BrushShape, "ブラシだけの形状");
    True(brushOnly.BrushMirror, "ブラシだけの左右反転");
    NearlyEqual(67, (float)brushOnly.CircleDiameterPixels.From, "ブラシだけの円直径", 0.001f);
    NearlyEqual(
        225,
        (float)brushOnly.EllipseWidthPixels.From,
        "ブラシだけの楕円幅",
        0.001f);
    NearlyEqual(
        250,
        (float)brushOnly.PngBrushSizeScale.From,
        "ブラシだけのPNG倍率",
        0.001f);
    Equal<GlassWipeQuality?>(preset.Quality, brushOnly.Quality, "ブラシだけの詳細品質");
    Equal<GlassWipeSimpleGeneratedQuality?>(
        preset.SimpleGeneratedQuality,
        brushOnly.SimpleGeneratedQuality,
        "ブラシだけの定型軌跡品質");

    True(
        DetailedWipePresetStateMerger.TryMerge(
            current,
            preset,
            DetailedWipePresetScope.All,
            out var all),
        "全設定を統合できる必要があります。");
    Equal(preset.CustomPathData, all.CustomPathData, "全設定のカスタム軌跡");
    Equal(GlassWipeBrushShape.Hand, all.BrushShape, "全設定のブラシ形状");
    NearlyEqual(250, (float)all.PngBrushSizeScale.From, "全設定のPNG倍率", 0.001f);
    Equal<GlassWipeQuality?>(preset.Quality, all.Quality, "全設定の詳細品質");
    Equal<GlassWipeSimpleGeneratedQuality?>(
        preset.SimpleGeneratedQuality,
        all.SimpleGeneratedQuality,
        "全設定の定型軌跡品質");

    True(
        preset.TrySanitize(DetailedWipePresetScope.Path, out var pathPreset),
        "軌跡プリセットを正規化できる必要があります。");
    True(pathPreset.Quality is null, "軌跡プリセットの詳細品質はnullにする必要があります。");
    True(
        pathPreset.SimpleGeneratedQuality is null,
        "軌跡プリセットの定型軌跡品質はnullにする必要があります。");
    var pathJson = JsonSerializer.Serialize(
        pathPreset,
        new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
    True(
        !pathJson.Contains("\"quality\"", StringComparison.Ordinal) &&
        !pathJson.Contains("\"simpleGeneratedQuality\"", StringComparison.Ordinal),
        "軌跡プリセットJSONへ品質キーを保存してはいけません。");

    var missingQuality = new DetailedWipePresetState
    {
        Quality = null,
        SimpleGeneratedQuality = null,
    };
    True(
        missingQuality.TrySanitize(DetailedWipePresetScope.Brush, out var defaultedQuality),
        "品質がない旧ブラシプリセットを補正できる必要があります。");
    Equal<GlassWipeQuality?>(
        GlassWipeQuality.Standard,
        defaultedQuality.Quality,
        "旧ブラシプリセットの詳細品質既定値");
    Equal<GlassWipeSimpleGeneratedQuality?>(
        GlassWipeSimpleGeneratedQuality.Auto,
        defaultedQuality.SimpleGeneratedQuality,
        "旧ブラシプリセットの定型軌跡品質既定値");

    var invalidPathBrush = new DetailedWipePresetState
    {
        CustomPathData = "broken",
        BrushShape = GlassWipeBrushShape.ShoePrint,
        OutsideDropletAmount = 33,
    };
    True(
        invalidPathBrush.TrySanitize(DetailedWipePresetScope.Brush, out _),
        "ブラシだけの登録はカスタム軌跡に依存してはいけません。");
    True(
        !invalidPathBrush.TrySanitize(DetailedWipePresetScope.Path, out _),
        "軌跡の登録は不正なカスタム軌跡を拒否する必要があります。");

    var simpleSnapshot = DetailedWipePresetExchangeCodec.EncodeSnapshot(
        WipePathInputMode.SimpleGenerated,
        current);
    True(
        DetailedWipePresetExchangeCodec.TryDecode(simpleSnapshot, out var decodedSnapshot),
        "かんたん作成中もブラシ登録用スナップショットを取得できる必要があります。");
    True(!decodedSnapshot.CanSavePath, "かんたん作成中は軌跡を登録できない必要があります。");
    True(decodedSnapshot.ApplyScope is null, "取得用スナップショットを適用要求にしてはいけません。");

    var brokenPathSnapshot = DetailedWipePresetExchangeCodec.EncodeSnapshot(
        WipePathInputMode.SimpleGenerated,
        invalidPathBrush);
    True(
        DetailedWipePresetExchangeCodec.TryDecode(
            brokenPathSnapshot,
            out var decodedBrokenPathSnapshot),
        "未使用のカスタム軌跡が破損していても現在設定を取得できる必要があります。");
    True(
        WipeStrokeDocumentCodec.TryDecode(
            decodedBrokenPathSnapshot.State.CustomPathData,
            out _),
        "現在設定の破損軌跡は安全な軌跡へ補正する必要があります。");
    NearlyEqual(
        33,
        (float)(decodedBrokenPathSnapshot.State.OutsideDropletAmount ?? -1),
        "破損軌跡の現在設定でも水滴量を保持",
        0.001f);

    var brushRequest = DetailedWipePresetExchangeCodec.EncodeApply(
        DetailedWipePresetScope.Brush,
        invalidPathBrush);
    True(
        DetailedWipePresetExchangeCodec.TryDecode(brushRequest, out var decodedRequest),
        "ブラシだけの適用要求を復元できる必要があります。");
    Equal(
        DetailedWipePresetScope.Brush,
        decodedRequest.ApplyScope,
        "ブラシだけの適用種類");
}

static void VerifyDetailedUserPresetStore()
{
    var temporaryDirectory = Path.Combine(
        Path.GetTempPath(),
        $"YMM4GlassWipe.Verification.{Guid.NewGuid():N}");
    Directory.CreateDirectory(temporaryDirectory);
    try
    {
        var filePath = Path.Combine(temporaryDirectory, "presets.json");
        var store = new DetailedWipeUserPresetStore(filePath);
        Equal(filePath, store.FilePath, "ユーザープリセット保存先");
        Equal(0, store.Load(out var missingError).Count, "未作成ファイルは空として扱う必要があります。");
        True(missingError is null, "未作成ファイルはエラーにしてはいけません。");

        var heart = DetailedWipePresetFactory.Create(DetailedWipeBuiltInPreset.Heart);
        True(
            store.Upsert(
                "  お気に入り  ",
                DetailedWipePresetScope.All,
                heart,
                out var replaced,
                out var error),
            error ?? "初回登録に失敗しました。");
        True(!replaced, "初回登録は置換扱いにしてはいけません。");
        True(File.Exists(filePath), "初回登録でプリセットファイルを作成する必要があります。");

        var loaded = store.Load(out error);
        True(error is null, error ?? "登録後の読込に失敗しました。");
        Equal(1, loaded.Count, "初回登録件数");
        Equal("お気に入り", loaded[0].Name, "プリセット名の前後空白を除去する必要があります。");
        var firstId = loaded[0].Id;

        var updated = DetailedWipePresetFactory.Create(DetailedWipeBuiltInPreset.Smiley);
        updated.BrushShape = GlassWipeBrushShape.Hand;
        updated.BrushMirror = true;
        updated.CircleDiameterPixels.From = 55;
        updated.CircleDiameterPixels.To = 55;
        updated.PngBrushSizeScale = DetailedWipePresetFactory.ConstantAnimation(175);
        True(
            store.Upsert(
                "お気に入り",
                DetailedWipePresetScope.All,
                updated,
                out replaced,
                out error),
            error ?? "上書き登録に失敗しました。");
        True(replaced, "同名登録は置換扱いにする必要があります。");
        True(File.Exists(filePath + ".bak"), "上書き時にバックアップを残す必要があります。");
        True(!File.Exists(filePath + ".tmp"), "正常保存後に一時ファイルを残してはいけません。");

        loaded = store.Load(out error);
        Equal(1, loaded.Count, "同名上書きで件数を増やしてはいけません。");
        Equal(firstId, loaded[0].Id, "同名上書きでプリセットIDを維持する必要があります。");
        Equal(
            GlassWipeBrushShape.Hand,
            loaded[0].State.BrushShape,
            "ユーザープリセットへブラシ形状を保存する必要があります。");
        True(
            loaded[0].State.BrushMirror,
            "ユーザープリセットへ左右反転を保存する必要があります。");
        NearlyEqual(55, (float)loaded[0].State.CircleDiameterPixels.From, "上書き後の円直径", 0.001f);
        NearlyEqual(
            175,
            (float)loaded[0].State.PngBrushSizeScale.From,
            "ユーザープリセットへPNG倍率を保存する必要があります。",
            0.001f);

        var scopedFilePath = Path.Combine(temporaryDirectory, "scoped", "presets.json");
        var scopedStore = new DetailedWipeUserPresetStore(scopedFilePath);
        True(
            scopedStore.Upsert(
                "共通名",
                DetailedWipePresetScope.Path,
                heart,
                out replaced,
                out error),
            error ?? "軌跡プリセットの登録に失敗しました。");
        True(
            scopedStore.Upsert(
                "共通名",
                DetailedWipePresetScope.Brush,
                updated,
                out replaced,
                out error),
            error ?? "ブラシプリセットの登録に失敗しました。");
        True(!replaced, "種類が違う同名プリセットは上書き扱いにしてはいけません。");
        var scopedPresets = scopedStore.Load(out error);
        Equal(2, scopedPresets.Count, "種類が違う同名プリセットは共存する必要があります。");
        Equal(
            1,
            scopedPresets.Count(preset => preset.Scope == DetailedWipePresetScope.Path),
            "軌跡プリセット件数");
        Equal(
            1,
            scopedPresets.Count(preset => preset.Scope == DetailedWipePresetScope.Brush),
            "ブラシプリセット件数");
        using (var scopedJson = JsonDocument.Parse(File.ReadAllText(scopedFilePath)))
        {
            Equal(
                DetailedWipeUserPresetFile.CurrentVersion,
                scopedJson.RootElement.GetProperty("dataSchemaVersion").GetInt32(),
                "ユーザープリセット外側スキーマ版");
            var savedPresets = scopedJson.RootElement.GetProperty("presets");
            var savedPathState = savedPresets.EnumerateArray()
                .Single(preset =>
                    preset.GetProperty("scope").GetInt32() ==
                    (int)DetailedWipePresetScope.Path)
                .GetProperty("state");
            True(
                !savedPathState.TryGetProperty("quality", out _) &&
                !savedPathState.TryGetProperty("simpleGeneratedQuality", out _),
                "Storeが保存した軌跡プリセットへ品質キーを含めてはいけません。");

            var savedBrushState = savedPresets.EnumerateArray()
                .Single(preset =>
                    preset.GetProperty("scope").GetInt32() ==
                    (int)DetailedWipePresetScope.Brush)
                .GetProperty("state");
            True(
                savedBrushState.TryGetProperty("quality", out _) &&
                savedBrushState.TryGetProperty("simpleGeneratedQuality", out _),
                "Storeが保存したブラシプリセットには両方の品質キーが必要です。");
            Equal(
                DetailedWipePresetState.CurrentVersion,
                savedBrushState.GetProperty("dataSchemaVersion").GetInt32(),
                "ユーザーブラシプリセット内側スキーマ版");
            True(
                savedBrushState.TryGetProperty("pngBrushSizeScale", out _),
                "Storeが保存したブラシプリセットにはPNG倍率が必要です。");
        }
        var scopedPathId = scopedPresets.Single(
            preset => preset.Scope == DetailedWipePresetScope.Path).Id;
        True(
            scopedStore.Upsert(
                "共通名",
                DetailedWipePresetScope.Path,
                updated,
                out replaced,
                out error),
            error ?? "同じ種類の上書きに失敗しました。");
        True(replaced, "同じ種類の同名プリセットは上書き扱いにする必要があります。");
        Equal(
            scopedPathId,
            scopedStore.Load(out error).Single(
                preset => preset.Scope == DetailedWipePresetScope.Path).Id,
            "同じ種類の上書きではIDを保持する必要があります。");
        True(
            !scopedStore.Upsert(
                "未知種類",
                (DetailedWipePresetScope)99,
                heart,
                out _,
                out error),
            "未知のプリセット種類を登録できてはいけません。");

        var compatibilityDirectory = Path.Combine(temporaryDirectory, "compatibility");
        var currentFilePath = Path.Combine(compatibilityDirectory, "current", "presets.json");
        var oldFilePath = Path.Combine(compatibilityDirectory, "legacy", "presets.json");
        var oldStore = new DetailedWipeUserPresetStore(oldFilePath);
        True(
            oldStore.Upsert(
                "旧保存先",
                DetailedWipePresetScope.Brush,
                updated,
                out replaced,
                out error),
            error ?? "旧保存先用プリセットの作成に失敗しました。");
        var compatibilityStore = new DetailedWipeUserPresetStore(
            currentFilePath,
            oldFilePath);
        var compatibilityPresets = compatibilityStore.Load(out error);
        True(error is null, error ?? "旧保存先からの互換読込に失敗しました。");
        Equal(1, compatibilityPresets.Count, "旧保存先からの互換読込件数");
        Equal("旧保存先", compatibilityPresets[0].Name, "旧保存先のプリセット名");
        True(
            compatibilityStore.Upsert(
                "新保存先",
                DetailedWipePresetScope.Path,
                heart,
                out replaced,
                out error),
            error ?? "新保存先への移行保存に失敗しました。");
        True(File.Exists(currentFilePath), "更新時はYMM4管理領域側へ保存する必要があります。");
        compatibilityPresets = compatibilityStore.Load(out error);
        Equal(2, compatibilityPresets.Count, "移行保存後のプリセット件数");
        File.WriteAllText(oldFilePath, "{broken-json");
        Equal(
            2,
            compatibilityStore.Load(out error).Count,
            "新保存先がある場合は旧保存先より優先する必要があります。");
        True(error is null, "新保存先の読込時に旧保存先の破損を伝播してはいけません。");

        var legacyFilePath = Path.Combine(temporaryDirectory, "legacy", "presets.json");
        Directory.CreateDirectory(Path.GetDirectoryName(legacyFilePath)!);
        var legacyJson = JsonSerializer.Serialize(new
        {
            dataSchemaVersion = 2,
            presets = new[]
            {
                new
                {
                    id = Guid.NewGuid(),
                    name = "旧形式",
                    scope = (int)DetailedWipePresetScope.Path,
                    state = new
                    {
                        dataSchemaVersion = 13,
                        customPathData = heart.CustomPathData,
                        brushSize = new { from = 30d, to = 30d, animationType = 0 },
                    },
                },
            },
        });
        File.WriteAllText(legacyFilePath, legacyJson);
        var legacyBytes = File.ReadAllBytes(legacyFilePath);
        var legacyStore = new DetailedWipeUserPresetStore(legacyFilePath);
        Equal(
            0,
            legacyStore.Load(out error).Count,
            "旧schemaと廃止済みブラシ項目を移行してはいけません。");
        True(
            error?.Contains("互換性", StringComparison.Ordinal) == true &&
            error?.Contains("登録し直", StringComparison.Ordinal) == true,
            "旧ユーザープリセットには再登録方法を表示する必要があります。");
        True(
            !legacyStore.Upsert(
                "上書き禁止",
                DetailedWipePresetScope.All,
                updated,
                out _,
                out error),
            "旧ユーザープリセットを暗黙に上書きしてはいけません。");
        True(
            File.ReadAllBytes(legacyFilePath).SequenceEqual(legacyBytes),
            "旧ユーザープリセットを変更してはいけません。");

        var previousStateFilePath = Path.Combine(
            temporaryDirectory,
            "previous-state",
            "presets.json");
        Directory.CreateDirectory(Path.GetDirectoryName(previousStateFilePath)!);
        File.WriteAllText(
            previousStateFilePath,
            JsonSerializer.Serialize(new
            {
                dataSchemaVersion = DetailedWipeUserPresetFile.CurrentVersion,
                presets = new[]
                {
                    new
                    {
                        id = Guid.NewGuid(),
                        name = "旧State",
                        scope = (int)DetailedWipePresetScope.Path,
                        state = new
                        {
                            dataSchemaVersion = DetailedWipePresetState.CurrentVersion - 1,
                            customPathData = heart.CustomPathData,
                        },
                    },
                },
            }));
        var previousStateStore = new DetailedWipeUserPresetStore(previousStateFilePath);
        Equal(
            0,
            previousStateStore.Load(out var previousStateError).Count,
            "現行外側schemaであっても旧Stateを受理してはいけません。");
        True(
            previousStateError?.Contains("互換性", StringComparison.Ordinal) == true,
            "旧Stateの非互換理由を表示する必要があります。");

        var unknownFilePath = Path.Combine(
            temporaryDirectory,
            "unknown-property",
            "presets.json");
        Directory.CreateDirectory(Path.GetDirectoryName(unknownFilePath)!);
        File.WriteAllText(
            unknownFilePath,
            JsonSerializer.Serialize(new
            {
                dataSchemaVersion = DetailedWipeUserPresetFile.CurrentVersion,
                presets = Array.Empty<object>(),
                unknownProperty = true,
            }));
        var unknownFileBytes = File.ReadAllBytes(unknownFilePath);
        var unknownStore = new DetailedWipeUserPresetStore(unknownFilePath);
        Equal(
            0,
            unknownStore.Load(out var unknownError).Count,
            "現行schemaでも未知項目を含むユーザープリセットを受理してはいけません。");
        True(
            unknownError?.Contains("互換性", StringComparison.Ordinal) == true,
            "未知項目を含むユーザープリセットには互換性エラーを表示する必要があります。");
        True(
            !unknownStore.Upsert(
                "上書き禁止",
                DetailedWipePresetScope.All,
                updated,
                out _,
                out _),
            "未知項目を含むユーザープリセットを暗黙に上書きしてはいけません。");
        True(
            File.ReadAllBytes(unknownFilePath).SequenceEqual(unknownFileBytes),
            "未知項目を含むユーザープリセットを変更してはいけません。");

        True(
            store.Upsert(
                "削除対象",
                DetailedWipePresetScope.All,
                DetailedWipePresetFactory.Create(DetailedWipeBuiltInPreset.LoveUmbrella),
                out replaced,
                out error),
            error ?? "2件目の登録に失敗しました。");
        var deleteTarget = store.Load(out error).Single(preset => preset.Name == "削除対象");
        True(store.Delete(deleteTarget.Id, out error), error ?? "削除に失敗しました。");
        Equal(1, store.Load(out error).Count, "指定プリセットだけを削除する必要があります。");
        True(!store.Delete(Guid.NewGuid(), out error), "存在しないプリセットの削除を拒否する必要があります。");
        True(error is not null, "存在しないプリセットの削除理由を返す必要があります。");

        True(
            !store.Upsert(
                string.Empty,
                DetailedWipePresetScope.All,
                heart,
                out _,
                out error),
            "空のプリセット名を拒否する必要があります。");
        True(error is not null, "空のプリセット名の拒否理由を返す必要があります。");
        True(
            !store.Upsert(
                new string('あ', DetailedWipeUserPresetStore.MaximumNameLength + 1),
                DetailedWipePresetScope.All,
                heart,
                out _,
                out error),
            "上限超過のプリセット名を拒否する必要があります。");
        True(
            !store.Upsert(
                "改行\n不可",
                DetailedWipePresetScope.All,
                heart,
                out _,
                out error),
            "制御文字を含むプリセット名を拒否する必要があります。");

        File.WriteAllText(filePath, "{broken-json");
        var brokenContents = File.ReadAllText(filePath);
        Equal(0, store.Load(out error).Count, "破損ファイルは空として安全に扱う必要があります。");
        True(error is not null, "破損ファイルの読込理由を返す必要があります。");
        True(
            !store.Upsert(
                "破損時登録",
                DetailedWipePresetScope.All,
                heart,
                out _,
                out error),
            "破損ファイルを暗黙に上書きしてはいけません。");
        Equal(brokenContents, File.ReadAllText(filePath), "破損ファイルを保持する必要があります。");

        var limitFilePath = Path.Combine(temporaryDirectory, "limit", "presets.json");
        var limitStore = new DetailedWipeUserPresetStore(limitFilePath);
        for (var index = 0; index < DetailedWipeUserPresetStore.MaximumPresetCount; index++)
        {
            True(
                limitStore.Upsert(
                    $"プリセット{index:D3}",
                    (index % 3) switch
                    {
                        0 => DetailedWipePresetScope.Path,
                        1 => DetailedWipePresetScope.Brush,
                        _ => DetailedWipePresetScope.All,
                    },
                    heart,
                    out _,
                    out error),
                error ?? $"上限試験の{index + 1}件目で登録に失敗しました。");
        }

        True(
            !limitStore.Upsert(
                "上限超過",
                DetailedWipePresetScope.Brush,
                heart,
                out _,
                out error),
            "100件を超えるユーザープリセットを拒否する必要があります。");
        Equal(
            DetailedWipeUserPresetStore.MaximumPresetCount,
            limitStore.Load(out error).Count,
            "上限拒否後も100件を保持する必要があります。");

        var oversizedPresetPath = Path.Combine(
            temporaryDirectory,
            "oversized-current",
            "presets.json");
        Directory.CreateDirectory(Path.GetDirectoryName(oversizedPresetPath)!);
        using (var oversizedPreset = new FileStream(
                   oversizedPresetPath,
                   FileMode.CreateNew,
                   FileAccess.Write,
                   FileShare.None))
        {
            oversizedPreset.SetLength(
                DetailedWipeUserPresetStore.MaximumFileBytes + 1L);
        }

        var oversizedPresetStore = new DetailedWipeUserPresetStore(
            oversizedPresetPath);
        var oversizedPresetLength = new FileInfo(oversizedPresetPath).Length;
        Equal(
            0,
            oversizedPresetStore.Load(out var oversizedPresetError).Count,
            "上限超過のユーザープリセットは空として扱う必要があります。");
        True(
            oversizedPresetError?.Contains("128 MiB", StringComparison.Ordinal) == true,
            "ユーザープリセットの容量超過理由を表示する必要があります。");
        True(
            !oversizedPresetStore.Upsert(
                "上書き禁止",
                DetailedWipePresetScope.All,
                heart,
                out _,
                out _),
            "上限超過のユーザープリセットを暗黙に上書きしてはいけません。");
        Equal(
            oversizedPresetLength,
            new FileInfo(oversizedPresetPath).Length,
            "上限超過のユーザープリセットを保持する必要があります。");

        var oversizedLegacyPath = Path.Combine(
            temporaryDirectory,
            "oversized-legacy",
            "presets.json");
        var oversizedLegacyCurrentPath = Path.Combine(
            temporaryDirectory,
            "oversized-legacy-current",
            "presets.json");
        Directory.CreateDirectory(Path.GetDirectoryName(oversizedLegacyPath)!);
        using (var oversizedLegacy = new FileStream(
                   oversizedLegacyPath,
                   FileMode.CreateNew,
                   FileAccess.Write,
                   FileShare.None))
        {
            oversizedLegacy.SetLength(
                DetailedWipeUserPresetStore.MaximumFileBytes + 1L);
        }

        var oversizedLegacyStore = new DetailedWipeUserPresetStore(
            oversizedLegacyCurrentPath,
            oversizedLegacyPath);
        Equal(
            0,
            oversizedLegacyStore.Load(out var oversizedLegacyError).Count,
            "上限超過の旧保存先も空として扱う必要があります。");
        True(oversizedLegacyError is not null, "旧保存先の容量超過理由を返す必要があります。");
        True(
            !oversizedLegacyStore.Upsert(
                "上書き禁止",
                DetailedWipePresetScope.All,
                heart,
                out _,
                out _),
            "上限超過の旧保存先を暗黙に移行保存してはいけません。");
        True(
            !File.Exists(oversizedLegacyCurrentPath),
            "旧保存先の読込失敗時に新保存先を作成してはいけません。");

        var invalidUtf8PresetPath = Path.Combine(
            temporaryDirectory,
            "invalid-utf8",
            "presets.json");
        Directory.CreateDirectory(Path.GetDirectoryName(invalidUtf8PresetPath)!);
        File.WriteAllBytes(invalidUtf8PresetPath, [0xFF]);
        var invalidUtf8PresetStore = new DetailedWipeUserPresetStore(
            invalidUtf8PresetPath);
        Equal(
            0,
            invalidUtf8PresetStore.Load(out var invalidUtf8PresetError).Count,
            "無効UTF-8のユーザープリセットは空として扱う必要があります。");
        True(invalidUtf8PresetError is not null, "無効UTF-8の読込理由を返す必要があります。");
        True(
            !invalidUtf8PresetStore.Upsert(
                "上書き禁止",
                DetailedWipePresetScope.All,
                heart,
                out _,
                out _),
            "無効UTF-8のユーザープリセットを暗黙に上書きしてはいけません。");
        True(
            File.ReadAllBytes(invalidUtf8PresetPath).SequenceEqual(new byte[] { 0xFF }),
            "無効UTF-8のユーザープリセットを保持する必要があります。");
    }
    finally
    {
        Directory.Delete(temporaryDirectory, recursive: true);
    }
}

static void VerifySimpleRegionIntegration()
{
    Equal(
        GlassWipeSimpleRegionShape.FullScreen,
        GlassWipeSimpleRegionShapeCompatibility.FromRegionShape(GlassWipeRegionShape.FullScreen),
        "全画面形状をかんたん作成の形状へ写像する必要があります。");
    Equal(
        GlassWipeSimpleRegionShape.Rectangle,
        GlassWipeSimpleRegionShapeCompatibility.FromRegionShape(GlassWipeRegionShape.Rectangle),
        "四角形をかんたん作成の形状へ写像する必要があります。");
    Equal(
        GlassWipeSimpleRegionShape.Ellipse,
        GlassWipeSimpleRegionShapeCompatibility.FromRegionShape(GlassWipeRegionShape.Ellipse),
        "円・楕円をかんたん作成の形状へ写像する必要があります。");
    Equal(
        GlassWipeSimpleRegionShape.Quad,
        GlassWipeSimpleRegionShapeCompatibility.FromRegionShape(GlassWipeRegionShape.Quad),
        "四隅指定をかんたん作成でも維持する必要があります。");
    Equal(
        GlassWipeSimpleRegionShape.Rectangle,
        GlassWipeSimpleRegionShapeCompatibility.FromRegionShape((GlassWipeRegionShape)99),
        "不正な保存領域形状はかんたん作成では四角形へフォールバックする必要があります。");

    Equal(
        GlassWipeRegionShape.FullScreen,
        GlassWipeSimpleRegionShapeCompatibility.ToRegionShape(GlassWipeSimpleRegionShape.FullScreen),
        "かんたん作成の全画面を保存対象の領域形状へ写像する必要があります。");
    Equal(
        GlassWipeRegionShape.Rectangle,
        GlassWipeSimpleRegionShapeCompatibility.ToRegionShape(GlassWipeSimpleRegionShape.Rectangle),
        "かんたん作成の四角形を保存対象の領域形状へ写像する必要があります。");
    Equal(
        GlassWipeRegionShape.Ellipse,
        GlassWipeSimpleRegionShapeCompatibility.ToRegionShape(GlassWipeSimpleRegionShape.Ellipse),
        "かんたん作成の円・楕円を保存対象の領域形状へ写像する必要があります。");
    Equal(
        GlassWipeRegionShape.Quad,
        GlassWipeSimpleRegionShapeCompatibility.ToRegionShape(GlassWipeSimpleRegionShape.Quad),
        "かんたん作成の四隅指定を保存対象の領域形状へ写像する必要があります。");
    Equal(
        GlassWipeRegionShape.Rectangle,
        GlassWipeSimpleRegionShapeCompatibility.ToRegionShape((GlassWipeSimpleRegionShape)99),
        "不正なかんたん作成の領域形状は四角形へフォールバックする必要があります。");

    var outsideQuad = new GlassWipeQuad(
        new Vector2(-0.25f, -0.1f),
        new Vector2(1.25f, -0.1f),
        new Vector2(1.15f, 1.2f),
        new Vector2(-0.15f, 1.2f));
    True(outsideQuad.TryCreateMapping(out var outsideMapping), "画面外座標を含む凸Quadを有効として扱う必要があります。");
    Vector2Equal(
        outsideQuad.TopLeft,
        outsideMapping.LocalToInput.Transform(Vector2.Zero),
        "画面外Quadの左上写像が不正です。");
}

static void VerifySimplePresetVisibility()
{
    Equal(
        "拭き取り量",
        GlassWipeSimpleVisibility.WipeAmountDisplayName,
        "かんたん作成の拭き取り量ラベルが不正です。");
    Equal(
        "ずれX",
        GlassWipeSimpleVisibility.ReturnOffsetXDisplayName,
        "かんたん作成のずれXラベルが不正です。");
    Equal(
        "ずれY",
        GlassWipeSimpleVisibility.ReturnOffsetYDisplayName,
        "かんたん作成のずれYラベルが不正です。");

    foreach (var pattern in Enum.GetValues<GlassWipeSimplePattern>())
    {
        foreach (var interpolation in Enum.GetValues<GlassWipePathInterpolation>())
        {
            var expectedControlPoint = pattern is
                GlassWipeSimplePattern.GentleArcOnce or
                GlassWipeSimplePattern.ArcRoundTrips ||
                pattern == GlassWipeSimplePattern.OffsetRoundTrips &&
                interpolation is
                    GlassWipePathInterpolation.Smooth or
                    GlassWipePathInterpolation.CircularArc;
            var expectedRoundTripSettings = pattern is
                GlassWipeSimplePattern.ShortRoundTrips or
                GlassWipeSimplePattern.ArcRoundTrips or
                GlassWipeSimplePattern.OffsetRoundTrips;
            var expectedReturnOffset =
                pattern == GlassWipeSimplePattern.OffsetRoundTrips;
            var expectedInterpolation = pattern is
                GlassWipeSimplePattern.GentleArcOnce or
                GlassWipeSimplePattern.ArcRoundTrips or
                GlassWipeSimplePattern.OffsetRoundTrips;

            Equal(
                expectedInterpolation,
                GlassWipeSimpleVisibility.IsInterpolationVisible(
                    GlassWipeEditingMode.Simple,
                    WipePathInputMode.LegacyAnimation,
                    pattern),
                $"{pattern}の補間方式表示が不正です。");
            Equal(
                expectedControlPoint,
                GlassWipeSimpleVisibility.IsControlPointVisible(
                    GlassWipeEditingMode.Simple,
                    WipePathInputMode.LegacyAnimation,
                    pattern,
                    interpolation),
                $"{pattern}と{interpolation}の経由点表示が不正です。");
            Equal(
                expectedRoundTripSettings,
                GlassWipeSimpleVisibility.AreRoundTripSettingsVisible(
                    GlassWipeEditingMode.Simple,
                    WipePathInputMode.LegacyAnimation,
                    pattern),
                $"{pattern}の往復設定表示が不正です。");
            Equal(
                expectedReturnOffset,
                GlassWipeSimpleVisibility.IsReturnOffsetVisible(
                    GlassWipeEditingMode.Simple,
                    WipePathInputMode.LegacyAnimation,
                    pattern),
                $"{pattern}のずらし量表示が不正です。");
        }
    }

    foreach (var editingMode in new[]
    {
        GlassWipeEditingMode.Detailed,
        (GlassWipeEditingMode)99,
    })
    {
        True(!GlassWipeSimpleVisibility.IsControlPointVisible(
                editingMode,
                WipePathInputMode.LegacyAnimation,
                GlassWipeSimplePattern.ArcRoundTrips,
                GlassWipePathInterpolation.CircularArc),
            $"{editingMode}でかんたん作成の経由点を表示してはいけません。");
        True(!GlassWipeSimpleVisibility.AreRoundTripSettingsVisible(
                editingMode,
                WipePathInputMode.LegacyAnimation,
                GlassWipeSimplePattern.ArcRoundTrips),
            $"{editingMode}でかんたん作成の往復設定を表示してはいけません。");
        True(!GlassWipeSimpleVisibility.IsReturnOffsetVisible(
                editingMode,
                WipePathInputMode.LegacyAnimation,
                GlassWipeSimplePattern.OffsetRoundTrips),
            $"{editingMode}でかんたん作成のずらし量を表示してはいけません。");
        True(!GlassWipeSimpleVisibility.IsInterpolationVisible(
                editingMode,
                WipePathInputMode.LegacyAnimation,
                GlassWipeSimplePattern.ArcRoundTrips),
            $"{editingMode}でかんたん作成の補間方式を表示してはいけません。");
    }

    True(!GlassWipeSimpleVisibility.IsControlPointVisible(
            GlassWipeEditingMode.Simple,
            WipePathInputMode.LegacyAnimation,
            (GlassWipeSimplePattern)99,
            (GlassWipePathInterpolation)99),
        "不正なプリセットで経由点を表示してはいけません。");
    True(!GlassWipeSimpleVisibility.AreRoundTripSettingsVisible(
            GlassWipeEditingMode.Simple,
            WipePathInputMode.LegacyAnimation,
            (GlassWipeSimplePattern)99),
        "不正なプリセットで往復設定を表示してはいけません。");
    True(!GlassWipeSimpleVisibility.IsReturnOffsetVisible(
            GlassWipeEditingMode.Simple,
            WipePathInputMode.LegacyAnimation,
            (GlassWipeSimplePattern)99),
        "不正なプリセットでずらし量を表示してはいけません。");

    True(GlassWipeSimpleVisibility.IsInterpolationVisible(
            GlassWipeEditingMode.Detailed,
            WipePathInputMode.SimpleGenerated,
            GlassWipeSimplePattern.ArcRoundTrips),
        "詳細設定の定型軌跡で補間方式を表示する必要があります。");
    True(GlassWipeSimpleVisibility.IsControlPointVisible(
            GlassWipeEditingMode.Detailed,
            WipePathInputMode.SimpleGenerated,
            GlassWipeSimplePattern.ArcRoundTrips,
            GlassWipePathInterpolation.CircularArc),
        "詳細設定の定型軌跡で経由点を表示する必要があります。");
    True(GlassWipeSimpleVisibility.AreRoundTripSettingsVisible(
            GlassWipeEditingMode.Detailed,
            WipePathInputMode.SimpleGenerated,
            GlassWipeSimplePattern.ArcRoundTrips),
        "詳細設定の定型軌跡で往復設定を表示する必要があります。");

    True(GlassWipeSimpleVisibility.IsRegionTransformVisible(
            GlassWipeEditingMode.Simple,
            GlassWipeRegionShape.Rectangle),
        "かんたん作成の四角形で中心・寸法を表示する必要があります。");
    True(!GlassWipeSimpleVisibility.IsRegionTransformVisible(
            GlassWipeEditingMode.Simple,
            GlassWipeRegionShape.Quad),
        "かんたん作成の四隅指定で未使用の中心・寸法を表示してはいけません。");
    True(GlassWipeSimpleVisibility.IsQuadConfigurationVisible(
            GlassWipeEditingMode.Simple,
            GlassWipeRegionShape.Quad),
        "かんたん作成の四隅指定でQuad設定を表示する必要があります。");
    True(GlassWipeSimpleVisibility.AreQuadPointsVisible(
            GlassWipeEditingMode.Simple,
            GlassWipeRegionShape.Quad,
            GlassWipeQuadPreset.Custom),
        "かんたん作成のカスタム四隅指定で4点を表示する必要があります。");
    True(!GlassWipeSimpleVisibility.AreQuadPointsVisible(
            GlassWipeEditingMode.Simple,
            GlassWipeRegionShape.Quad,
            GlassWipeQuadPreset.FrontWindshield),
        "かんたん作成のフロントガラスプリセットで未使用の4点を表示してはいけません。");

    VerifyRegionVisibilityTable();
}

static void VerifyRegionVisibilityTable()
{
    foreach (var editingMode in new[]
             {
                 GlassWipeEditingMode.Simple,
                 GlassWipeEditingMode.Detailed,
             })
    {
        foreach (var regionShape in Enum.GetValues<GlassWipeRegionShape>())
        {
            var isTransformVisible = regionShape is
                GlassWipeRegionShape.Rectangle or GlassWipeRegionShape.Ellipse;
            var isQuadVisible = regionShape == GlassWipeRegionShape.Quad;
            Equal(
                isTransformVisible,
                GlassWipeSimpleVisibility.IsRegionTransformVisible(editingMode, regionShape),
                $"{editingMode}/{regionShape}の中心・寸法表示が不正です。");
            Equal(
                isQuadVisible,
                GlassWipeSimpleVisibility.IsQuadConfigurationVisible(editingMode, regionShape),
                $"{editingMode}/{regionShape}の四隅プリセット表示が不正です。");

            foreach (var quadPreset in Enum.GetValues<GlassWipeQuadPreset>())
            {
                Equal(
                    isQuadVisible && quadPreset == GlassWipeQuadPreset.Custom,
                    GlassWipeSimpleVisibility.AreQuadPointsVisible(
                        editingMode,
                        regionShape,
                        quadPreset),
                    $"{editingMode}/{regionShape}/{quadPreset}の四隅座標表示が不正です。");
            }
        }

        True(
            GlassWipeSimpleVisibility.IsRegionTransformVisible(
                editingMode,
                (GlassWipeRegionShape)99) &&
            !GlassWipeSimpleVisibility.IsQuadConfigurationVisible(
                editingMode,
                (GlassWipeRegionShape)99) &&
            !GlassWipeSimpleVisibility.AreQuadPointsVisible(
                editingMode,
                (GlassWipeRegionShape)99,
                GlassWipeQuadPreset.Custom),
            $"{editingMode}の不正な領域形状は四角形として扱う必要があります。");
        True(
            GlassWipeSimpleVisibility.AreQuadPointsVisible(
                editingMode,
                GlassWipeRegionShape.Quad,
                (GlassWipeQuadPreset)99),
            $"{editingMode}の不正な四隅プリセットはカスタムとして扱う必要があります。");
    }

    var invalidEditingMode = (GlassWipeEditingMode)99;
    foreach (var regionShape in Enum.GetValues<GlassWipeRegionShape>())
    {
        foreach (var quadPreset in Enum.GetValues<GlassWipeQuadPreset>())
        {
            True(
                !GlassWipeSimpleVisibility.IsRegionTransformVisible(invalidEditingMode, regionShape) &&
                !GlassWipeSimpleVisibility.IsQuadConfigurationVisible(invalidEditingMode, regionShape) &&
                !GlassWipeSimpleVisibility.AreQuadPointsVisible(
                    invalidEditingMode,
                    regionShape,
                    quadPreset),
                $"不正な編集モードで{regionShape}/{quadPreset}の領域設定を表示してはいけません。");
        }
    }
}

static void VerifySimplePathPatternsAndInterpolation()
{
    var start = new Vector2(10, 75);
    var control = new Vector2(50, 15);
    var end = new Vector2(90, 70);

    foreach (var pattern in Enum.GetValues<GlassWipeSimplePattern>())
    {
        var first = WipeSimplePathGenerator.Generate(
            pattern, GlassWipePathInterpolation.Linear, start, control, end, 3, 120, 120);
        var second = WipeSimplePathGenerator.Generate(
            pattern, GlassWipePathInterpolation.Linear, start, control, end, 3, 120, 120);

        AssertSimplePathResult(first, 120, 120, $"{pattern}の生成結果");
        True(first.Samples.SequenceEqual(second.Samples), $"{pattern}は決定論的なサンプル列を返す必要があります。");
        Equal(first.Signature, second.Signature, $"{pattern}は決定論的な署名を返す必要があります。");
        var expectedStyle = pattern is
            GlassWipeSimplePattern.ShortRoundTrips or
            GlassWipeSimplePattern.ArcRoundTrips or
            GlassWipeSimplePattern.OffsetRoundTrips
                ? WipeSimplePathGenerator.FixedProgressiveStyle
                : WipeSimplePathGenerator.FixedStyle;
        var expectedQuality = pattern is
            GlassWipeSimplePattern.ShortRoundTrips or
            GlassWipeSimplePattern.ArcRoundTrips or
            GlassWipeSimplePattern.OffsetRoundTrips
                ? GlassWipeQuality.High
                : GlassWipeQuality.Standard;
        Equal(
            expectedQuality,
            first.Style.Quality,
            $"{pattern}の固定スタンプ密度が不正です。");
        Equal(
            expectedStyle,
            first.Style with
            {
                InitialTangentX = 0,
                InitialTangentY = 0,
            },
            $"{pattern}は開始方向以外の固定描画スタイルを維持する必要があります。");
        True(
            float.IsFinite(first.Style.InitialTangentX) &&
            float.IsFinite(first.Style.InitialTangentY) &&
            new Vector2(
                first.Style.InitialTangentX,
                first.Style.InitialTangentY).LengthSquared() > 0,
            $"{pattern}は有限な開始方向を保持する必要があります。");
        Equal(first.Style, second.Style, $"{pattern}は決定論的な開始方向を返す必要があります。");
    }

    var linear = WipeSimplePathGenerator.Generate(
        GlassWipeSimplePattern.GentleArcOnce, GlassWipePathInterpolation.Linear, start, control, end, 1, 120, 120);
    var smooth = WipeSimplePathGenerator.Generate(
        GlassWipeSimplePattern.GentleArcOnce, GlassWipePathInterpolation.Smooth, start, control, end, 1, 120, 120);
    True(!linear.Samples.SequenceEqual(smooth.Samples), "経由点を含む軌跡では直線補間となめらか補間が異なる必要があります。");

    foreach (var straightPattern in new[]
    {
        GlassWipeSimplePattern.StraightOnce,
        GlassWipeSimplePattern.ShortRoundTrips,
    })
    {
        var fixedLinear = WipeSimplePathGenerator.Generate(
            straightPattern, GlassWipePathInterpolation.Linear, start, control, end, 2, 120, 120);
        var hiddenSmooth = WipeSimplePathGenerator.Generate(
            straightPattern, GlassWipePathInterpolation.Smooth, start, control, end, 2, 120, 120);
        var hiddenArc = WipeSimplePathGenerator.Generate(
            straightPattern, GlassWipePathInterpolation.CircularArc, start, control, end, 2, 120, 120);
        True(fixedLinear.Samples.SequenceEqual(hiddenSmooth.Samples), $"{straightPattern}はなめらか指定を無視して直線固定にする必要があります。");
        True(fixedLinear.Samples.SequenceEqual(hiddenArc.Samples), $"{straightPattern}は円弧指定を無視して直線固定にする必要があります。");
        Equal(fixedLinear.Signature, hiddenSmooth.Signature, $"{straightPattern}の非表示補間値は署名へ影響してはいけません。");
        Equal(fixedLinear.Signature, hiddenArc.Signature, $"{straightPattern}の非表示円弧値は署名へ影響してはいけません。");
    }
}

static void VerifyArcRoundTripSmoothRetracing()
{
    const int framesPerPass = 60;
    var start = new Vector2(15, 65);
    var control = new Vector2(50, 25);
    var end = new Vector2(85, 65);

    for (var roundTrips = 1; roundTrips <= 5; roundTrips++)
    {
        var itemLength = roundTrips * framesPerPass * 2;
        var smooth = WipeSimplePathGenerator.Generate(
            GlassWipeSimplePattern.ArcRoundTrips,
            GlassWipePathInterpolation.Smooth,
            start,
            control,
            end,
            roundTrips,
            itemLength,
            itemLength);
        var linear = WipeSimplePathGenerator.Generate(
            GlassWipeSimplePattern.ArcRoundTrips,
            GlassWipePathInterpolation.Linear,
            start,
            control,
            end,
            roundTrips,
            itemLength,
            itemLength);

        AssertSimplePathResult(
            smooth,
            itemLength,
            itemLength,
            $"なめらかな経由点あり往復の{roundTrips}往復");
        True(
            !smooth.Samples.SequenceEqual(linear.Samples),
            "経由点あり往復のなめらか補間は直線補間と異なる必要があります。");

        for (var trip = 0; trip < roundTrips; trip++)
        {
            var tripStart = trip * framesPerPass * 2;
            for (var offset = 0; offset <= framesPerPass; offset++)
            {
                Vector2Equal(
                    GetSimplePosition(smooth, tripStart + offset),
                    GetSimplePosition(
                        smooth,
                        tripStart + framesPerPass * 2 - offset),
                    $"なめらかな経由点あり往復の{trip + 1}往復目で往路と復路が一致しません。",
                    0.00001f);
            }
        }
    }
}

static void VerifySimpleProgress()
{
    const int itemLength = 120;
    var start = new Vector2(10, 50);
    var control = new Vector2(50, 20);
    var end = new Vector2(90, 50);
    var linear = WipeSimplePathGenerator.Generate(
        GlassWipeSimplePattern.StraightOnce,
        GlassWipePathInterpolation.Linear,
        start,
        control,
        end,
        1,
        itemLength,
        itemLength);
    var accelerated = WipeSimplePathGenerator.Generate(
        GlassWipeSimplePattern.StraightOnce,
        GlassWipePathInterpolation.Linear,
        start,
        control,
        end,
        1,
        itemLength,
        itemLength,
        progressProvider: frame => MathF.Pow(frame / (float)itemLength, 2));

    Vector2Equal(start / 100, GetSimplePosition(accelerated, 0), "進行度Animationの始点が不正です。");
    Vector2Equal(end / 100, GetSimplePosition(accelerated, itemLength), "進行度Animationの終点が不正です。");
    NearlyEqual(0.5f, linear.Samples[itemLength / 2].X, "直線進行の中間位置が不正です。");
    NearlyEqual(0.3f, accelerated.Samples[itemLength / 2].X, "加速進行の中間位置が不正です。");
    True(!linear.Samples.SequenceEqual(accelerated.Samples), "進行度Animationをサンプル列へ反映する必要があります。");

    var clamped = WipeSimplePathGenerator.Generate(
        GlassWipeSimplePattern.StraightOnce,
        GlassWipePathInterpolation.Linear,
        start,
        control,
        end,
        1,
        3,
        3,
        progressProvider: frame => frame switch
        {
            0 => float.NaN,
            1 => -1,
            _ => 2,
        });
    Vector2Equal(start / 100, GetSimplePosition(clamped, 0), "非有限の進行度は始点へ補正する必要があります。");
    Vector2Equal(start / 100, GetSimplePosition(clamped, 1), "負の進行度は始点へ補正する必要があります。");
    Vector2Equal(end / 100, GetSimplePosition(clamped, 2), "100%超の進行度は終点へ補正する必要があります。");
}

static void VerifyPathCompletionAndHold()
{
    NearlyEqual(
        50,
        (float)WipePathTiming.DefaultCompletionPercent,
        "描画完了位置の既定値",
        0.001f);
    NearlyEqual(
        50,
        (float)WipePathTiming.SanitizeCompletionPercent(double.NaN),
        "非有限の描画完了位置は既定値へ補正する必要があります。",
        0.001f);
    NearlyEqual(
        100,
        (float)WipePathTiming.ResolveAfterDeserialization(
            completionWasSet: false,
            WipePathTiming.DefaultCompletionPercent),
        "描画完了位置がない旧保存データは100%を維持する必要があります。",
        0.001f);
    NearlyEqual(
        50,
        (float)WipePathTiming.ResolveAfterDeserialization(
            completionWasSet: true,
            WipePathTiming.DefaultCompletionPercent),
        "明示された新しい既定値50%を保持する必要があります。",
        0.001f);
    NearlyEqual(
        1,
        (float)WipePathTiming.SanitizeCompletionPercent(-20),
        "描画完了位置の下限補正",
        0.001f);
    NearlyEqual(
        100,
        (float)WipePathTiming.SanitizeCompletionPercent(120),
        "描画完了位置の上限補正",
        0.001f);
    Equal(0, WipePathTiming.GetCompletionFrame(0, 80), "長さ0の完了フレーム");
    Equal(1, WipePathTiming.GetCompletionFrame(1, 1), "正の長さでは1フレーム以上を確保する必要があります。");
    Equal(90, WipePathTiming.GetCompletionFrame(120, 75), "75%の完了フレーム");
    Equal(120, WipePathTiming.GetCompletionFrame(120, 100), "100%は従来の最終フレームを維持する必要があります。");

    const int itemLength = 120;
    const int completionFrame = 90;
    var heldHistoryFrame = WipePathSnapshot.GetLastHistoryFrame(
        itemLength,
        completionFrame);
    Equal(completionFrame, heldHistoryFrame, "維持区間では履歴を完了フレームへ固定する必要があります。");

    var simpleAtCompletion = WipeSimplePathGenerator.Generate(
        GlassWipeSimplePattern.StraightOnce,
        GlassWipePathInterpolation.Linear,
        new Vector2(10, 50),
        new Vector2(50, 20),
        new Vector2(90, 50),
        1,
        completionFrame,
        completionFrame);
    var simpleDuringHold = WipeSimplePathGenerator.Generate(
        GlassWipeSimplePattern.StraightOnce,
        GlassWipePathInterpolation.Linear,
        new Vector2(10, 50),
        new Vector2(50, 20),
        new Vector2(90, 50),
        1,
        heldHistoryFrame,
        completionFrame);
    True(
        simpleAtCompletion.Samples.SequenceEqual(simpleDuringHold.Samples),
        "かんたん作成の維持区間で軌跡サンプルを追加してはいけません。");
    NearlyEqual(0.9f, simpleDuringHold.Samples[^1].X, "維持区間の終点X", 0.001f);
    NearlyEqual(0.5f, simpleDuringHold.Samples[^1].Y, "維持区間の終点Y", 0.001f);

    var customDocument = new WipeStrokeDocument
    {
        Strokes =
        [
            new WipeStroke
            {
                Points =
                [
                    PercentStrokePoint(0, 10, 50, 100),
                    PercentStrokePoint(100, 90, 50, 100),
                ],
            },
        ],
    };
    var customAtCompletion = WipeStrokeSampler.Expand(
        customDocument,
        completionFrame,
        completionFrame);
    var customDuringHold = WipeStrokeSampler.Expand(
        customDocument,
        heldHistoryFrame,
        completionFrame);
    True(
        customAtCompletion.SequenceEqual(customDuringHold),
        "カスタム軌跡の維持区間で軌跡サンプルを追加してはいけません。");
    Equal(completionFrame, customDuringHold[^1].Frame, "カスタム軌跡の完了フレーム");
}

static void VerifyPathTimingModes()
{
    Equal(3, Enum.GetValues<WipePathTimingMode>().Length, "時間指定方式数が不正です。");
    Equal(0, (int)WipePathTimingMode.Percent, "割合のenum値が不正です。");
    Equal(1, (int)WipePathTimingMode.Frame, "固定フレームのenum値が不正です。");
    Equal(2, (int)WipePathTimingMode.Seconds, "秒数のenum値が不正です。");
    Equal("パーセンテージ", GetDisplayName(WipePathTimingMode.Percent), "パーセンテージの表示名が不正です。");
    Equal("固定フレーム", GetDisplayName(WipePathTimingMode.Frame), "固定フレームの表示名が不正です。");
    Equal("秒数", GetDisplayName(WipePathTimingMode.Seconds), "秒数の表示名が不正です。");
    Equal(
        WipePathTimingMode.Percent,
        WipePathTimingModeCompatibility.Default,
        "既定の時間指定方式は割合である必要があります。");
    Equal(
        WipePathTimingMode.Percent,
        WipePathTimingModeCompatibility.Normalize((WipePathTimingMode)99),
        "不正な時間指定方式は割合へ補正する必要があります。");

    True(
        WipePathTimingVisibility.IsPercentVisible(WipePathTimingMode.Percent) &&
        !WipePathTimingVisibility.IsFrameVisible(WipePathTimingMode.Percent) &&
        !WipePathTimingVisibility.IsSecondsVisible(WipePathTimingMode.Percent),
        "割合では割合の入力欄だけを表示する必要があります。");
    True(
        !WipePathTimingVisibility.IsPercentVisible(WipePathTimingMode.Frame) &&
        WipePathTimingVisibility.IsFrameVisible(WipePathTimingMode.Frame) &&
        !WipePathTimingVisibility.IsSecondsVisible(WipePathTimingMode.Frame),
        "固定フレームではフレーム入力欄だけを表示する必要があります。");
    True(
        !WipePathTimingVisibility.IsPercentVisible(WipePathTimingMode.Seconds) &&
        !WipePathTimingVisibility.IsFrameVisible(WipePathTimingMode.Seconds) &&
        WipePathTimingVisibility.IsSecondsVisible(WipePathTimingMode.Seconds),
        "秒数では秒入力欄だけを表示する必要があります。");

    NearlyEqual(0, (float)WipePathTiming.DefaultStartPercent, "割合の既定開始位置");
    NearlyEqual(50, (float)WipePathTiming.DefaultCompletionPercent, "割合の既定完了位置");
    NearlyEqual(0, (float)WipePathTiming.DefaultStartFrame, "固定フレームの既定開始位置");
    NearlyEqual(60, (float)WipePathTiming.DefaultCompletionFrame, "固定フレームの既定完了位置");
    NearlyEqual(0, (float)WipePathTiming.DefaultStartSeconds, "秒数の既定開始位置");
    NearlyEqual(1, (float)WipePathTiming.DefaultCompletionSeconds, "秒数の既定完了位置");
    NearlyEqual(
        60,
        (float)WipePathTiming.SanitizeFixedFrame(
            double.NaN,
            WipePathTiming.DefaultCompletionFrame),
        "非有限の完了フレームは完了側の既定値へ補正する必要があります。");
    NearlyEqual(
        1,
        (float)WipePathTiming.SanitizeSeconds(
            double.PositiveInfinity,
            WipePathTiming.DefaultCompletionSeconds),
        "非有限の完了秒は完了側の既定値へ補正する必要があります。");

    var percent = WipePathTiming.ResolveWindow(
        120,
        60,
        WipePathTimingMode.Percent,
        20,
        50,
        0,
        60,
        0,
        1);
    Equal(24, percent.StartFrame, "割合指定の開始フレーム");
    Equal(60, percent.CompletionFrame, "割合指定の完了フレーム");
    Equal(36, percent.EvaluationLength, "割合指定の描画区間長");
    Equal(-1, percent.GetEvaluationFrame(23), "開始前は軌跡を生成してはいけません。");
    Equal(0, percent.GetEvaluationFrame(24), "開始位置は軌跡の先頭である必要があります。");
    Equal(18, percent.GetEvaluationFrame(42), "割合指定の中間進行位置");
    Equal(36, percent.GetEvaluationFrame(60), "割合指定の完了位置");
    Equal(36, percent.GetEvaluationFrame(120), "完了後は最終状態を維持する必要があります。");

    var fixedFrames = WipePathTiming.ResolveWindow(
        100,
        60,
        WipePathTimingMode.Frame,
        0,
        50,
        10,
        180,
        0,
        1);
    Equal(10, fixedFrames.StartFrame, "固定フレーム指定の開始位置");
    Equal(180, fixedFrames.CompletionFrame, "固定フレーム指定をアイテム長で切り詰めてはいけません。");
    Equal(170, fixedFrames.EvaluationLength, "固定フレーム指定の描画区間長");
    Equal(90, fixedFrames.GetEvaluationFrame(100), "アイテム終端で未完了の進行位置を保持する必要があります。");

    var secondsAt60Fps = WipePathTiming.ResolveWindow(
        120,
        60,
        WipePathTimingMode.Seconds,
        0,
        50,
        0,
        60,
        0.5,
        1.5);
    Equal(30, secondsAt60Fps.StartFrame, "60fpsで0.5秒の開始位置");
    Equal(90, secondsAt60Fps.CompletionFrame, "60fpsで1.5秒の完了位置");
    var secondsAt30Fps = WipePathTiming.ResolveWindow(
        120,
        30,
        WipePathTimingMode.Seconds,
        0,
        50,
        0,
        60,
        0.5,
        1.5);
    Equal(15, secondsAt30Fps.StartFrame, "30fpsで0.5秒の開始位置");
    Equal(45, secondsAt30Fps.CompletionFrame, "30fpsで1.5秒の完了位置");

    var immediate = WipePathTiming.ResolveWindow(
        120,
        60,
        WipePathTimingMode.Frame,
        0,
        50,
        80,
        40,
        0,
        1);
    True(immediate.IsImmediate, "開始位置が完了位置以上なら即時完了として扱う必要があります。");
    Equal(-1, immediate.GetEvaluationFrame(79), "即時完了でも開始前は空である必要があります。");
    Equal(
        immediate.EvaluationLength,
        immediate.GetEvaluationFrame(80),
        "開始位置で完成状態へ切り替える必要があります。");

    var source = new WipePathSnapshot(
        5,
        10,
        60,
        [new WipePathSample(0, 0.5f, 0.5f, 1, 0.1f, 1, 1, 0)],
        new WipePathStyle(0.2f, 0.01f, 7),
        WipePathInputMode.StrokeCollection,
        "timing-test");
    var beforeStart = WipePathSnapshot.ApplyStartGate(source, hasStarted: false);
    Equal(0, beforeStart.Samples.Length, "開始前のスナップショットは空である必要があります。");
    Equal(source.ItemLength, beforeStart.ItemLength, "開始前も描画区間長を保持する必要があります。");
    Equal(source.Style, beforeStart.Style, "開始前もスタイルを保持する必要があります。");
    Equal(source.InputMode, beforeStart.InputMode, "開始前も軌跡入力方式を保持する必要があります。");
    Equal(source.PathDataSignature, beforeStart.PathDataSignature, "開始前も軌跡署名を保持する必要があります。");
    True(
        ReferenceEquals(source, WipePathSnapshot.ApplyStartGate(source, hasStarted: true)),
        "開始後のスナップショットを作り直してはいけません。");
}

static void VerifyThreePointCircularArcInterpolation()
{
    foreach (var geometry in new[]
    {
        WipeMaskGeometry.Create(1600, 900),
        WipeMaskGeometry.Create(900, 1600),
        WipeMaskGeometry.Create(1000, 1000),
    })
    {
        var physicalCenter = geometry.ToShortSideVector(new Vector2(0.5f));
        const float radius = 0.22f;
        var start = geometry.FromShortSideVector(
            physicalCenter + new Vector2(-radius, 0)) * 100;
        var control = geometry.FromShortSideVector(
            physicalCenter + new Vector2(0, -radius)) * 100;
        var end = geometry.FromShortSideVector(
            physicalCenter + new Vector2(radius, 0)) * 100;
        var result = WipeSimplePathGenerator.Generate(
            GlassWipeSimplePattern.GentleArcOnce,
            GlassWipePathInterpolation.CircularArc,
            start,
            control,
            end,
            1,
            100,
            100,
            maskGeometry: geometry);

        AssertSimplePathResult(result, 100, 100, $"{geometry}の3点円弧");
        Vector2Equal(start / 100, GetSimplePosition(result, 0), "3点円弧の始点が不正です。", 0.00001f);
        Vector2Equal(control / 100, GetSimplePosition(result, 50), "3点円弧が経由点を通っていません。", 0.00001f);
        Vector2Equal(end / 100, GetSimplePosition(result, 100), "3点円弧の終点が不正です。", 0.00001f);
        foreach (var sample in result.Samples)
        {
            var physicalPosition = geometry.ToShortSideVector(
                new Vector2(sample.X, sample.Y));
            NearlyEqual(
                radius,
                Vector2.Distance(physicalCenter, physicalPosition),
                $"{geometry}の3点円弧は物理座標で一定半径を維持する必要があります。",
                0.00005f);
        }
    }

    var square = WipeMaskGeometry.Create(1000, 1000);
    var smallArcCenter = new Vector2(0.5f);
    const float smallArcRadius = 0.25f;
    var smallArcStart = (smallArcCenter + new Vector2(smallArcRadius, 0)) * 100;
    var smallArcControl = (smallArcCenter + new Vector2(
        smallArcRadius * MathF.Cos(MathF.PI / 4),
        -smallArcRadius * MathF.Sin(MathF.PI / 4))) * 100;
    var smallArcEnd = (smallArcCenter + new Vector2(0, -smallArcRadius)) * 100;
    var smallArc = WipeSimplePathGenerator.Generate(
        GlassWipeSimplePattern.GentleArcOnce,
        GlassWipePathInterpolation.CircularArc,
        smallArcStart,
        smallArcControl,
        smallArcEnd,
        1,
        100,
        100,
        maskGeometry: square);
    var expectedSmallArcQuarter = smallArcCenter + new Vector2(
        smallArcRadius * MathF.Cos(MathF.PI / 8),
        -smallArcRadius * MathF.Sin(MathF.PI / 8));
    Vector2Equal(expectedSmallArcQuarter, GetSimplePosition(smallArc, 25),
        "3点円弧は経由点を含む180度未満の小弧を選択する必要があります。", 0.00001f);
    Vector2Equal(smallArcControl / 100, GetSimplePosition(smallArc, 50),
        "小弧が指定した経由点を通っていません。", 0.00001f);

    var majorArc = WipeSimplePathGenerator.Generate(
        GlassWipeSimplePattern.GentleArcOnce,
        GlassWipePathInterpolation.CircularArc,
        new Vector2(75, 50),
        new Vector2(25, 50),
        new Vector2(50, 75),
        1,
        100,
        100,
        maskGeometry: square);
    Vector2Equal(new Vector2(0.5f, 0.25f), GetSimplePosition(majorArc, 25),
        "3点円弧は経由点を含む大弧を選択する必要があります。", 0.00001f);
    Vector2Equal(new Vector2(0.25f, 0.5f), GetSimplePosition(majorArc, 50),
        "大弧が指定した経由点を通っていません。", 0.00001f);

    var wide = WipeMaskGeometry.Create(1600, 900);
    var center = wide.ToShortSideVector(new Vector2(0.5f));
    const float roundTripRadius = 0.2f;
    var roundTripStart = wide.FromShortSideVector(
        center + new Vector2(-roundTripRadius, 0)) * 100;
    var roundTripControl = wide.FromShortSideVector(
        center + new Vector2(0, -roundTripRadius)) * 100;
    var roundTripEnd = wide.FromShortSideVector(
        center + new Vector2(roundTripRadius, 0)) * 100;

    for (var roundTrips = 1; roundTrips <= 5; roundTrips++)
    {
        var itemLength = roundTrips * 120;
        var result = WipeSimplePathGenerator.Generate(
            GlassWipeSimplePattern.ArcRoundTrips,
            GlassWipePathInterpolation.CircularArc,
            roundTripStart,
            roundTripControl,
            roundTripEnd,
            roundTrips,
            itemLength,
            itemLength,
            maskGeometry: wide);
        AssertSimplePathResult(
            result,
            itemLength,
            itemLength,
            $"3点円弧の{roundTrips}往復");

        for (var trip = 0; trip < roundTrips; trip++)
        {
            var tripStart = trip * 120;
            Vector2Equal(roundTripStart / 100, GetSimplePosition(result, tripStart),
                $"3点円弧の{trip + 1}往復目の始点が不正です。", 0.00001f);
            Vector2Equal(roundTripControl / 100, GetSimplePosition(result, tripStart + 30),
                $"3点円弧の{trip + 1}往復目の往路経由点が不正です。", 0.00001f);
            Vector2Equal(roundTripEnd / 100, GetSimplePosition(result, tripStart + 60),
                $"3点円弧の{trip + 1}往復目の折返し点が不正です。", 0.00001f);
            Vector2Equal(roundTripControl / 100, GetSimplePosition(result, tripStart + 90),
                $"3点円弧の{trip + 1}往復目の復路経由点が不正です。", 0.00001f);
            Vector2Equal(roundTripStart / 100, GetSimplePosition(result, tripStart + 120),
                $"3点円弧の{trip + 1}往復目の終点が不正です。", 0.00001f);
            Equal(trip * 2, result.Samples[tripStart + 60].AccumulationGroup,
                $"3点円弧の{trip + 1}往復目の折返し到達フレームは往路グループを維持する必要があります。");
            Equal(trip * 2 + 1, result.Samples[tripStart + 61].AccumulationGroup,
                $"3点円弧の{trip + 1}往復目の折返し後は復路グループへ進む必要があります。");
        }

        True(result.Samples.All(sample => MathF.Abs(sample.Strength - 0.4f) <= 0.000001f),
            "3点円弧の往復でも1回あたりの拭き取り量を維持する必要があります。");
    }

    const int nonDivisibleItemLength = 127;
    var nonDivisible = WipeSimplePathGenerator.Generate(
        GlassWipeSimplePattern.ArcRoundTrips,
        GlassWipePathInterpolation.CircularArc,
        roundTripStart,
        roundTripControl,
        roundTripEnd,
        5,
        nonDivisibleItemLength,
        nonDivisibleItemLength,
        maskGeometry: wide);
    AssertSimplePathResult(
        nonDivisible,
        nonDivisibleItemLength,
        nonDivisibleItemLength,
        "3点円弧の非整除フレーム長");
    Vector2Equal(roundTripStart / 100, GetSimplePosition(nonDivisible, nonDivisibleItemLength),
        "非整除フレーム長でも3点円弧の最終位置は始点へ戻る必要があります。", 0.00001f);
    Equal(9, nonDivisible.Samples[^1].AccumulationGroup,
        "非整除フレーム長でも最終片道グループを維持する必要があります。");
    for (var transition = 1; transition < 10; transition++)
    {
        var frameBeforeTransition = transition * nonDivisibleItemLength / 10;
        Equal(transition - 1, nonDivisible.Samples[frameBeforeTransition].AccumulationGroup,
            $"非整除フレーム長の第{transition}境界直前グループが不正です。");
        Equal(transition, nonDivisible.Samples[frameBeforeTransition + 1].AccumulationGroup,
            $"非整除フレーム長の第{transition}境界直後グループが不正です。");
    }

    foreach (var currentFrame in new[] { 0, 1, 13, 64, 126, 127 })
    {
        var direct = WipeSimplePathGenerator.Generate(
            GlassWipeSimplePattern.ArcRoundTrips,
            GlassWipePathInterpolation.CircularArc,
            roundTripStart,
            roundTripControl,
            roundTripEnd,
            5,
            currentFrame,
            nonDivisibleItemLength,
            maskGeometry: wide);
        True(direct.Samples.SequenceEqual(nonDivisible.Samples.Take(currentFrame + 1)),
            "非整除フレーム長の直接シーク結果は完全生成した軌跡のprefixと一致する必要があります。");
    }

    var offset = WipeSimplePathGenerator.Generate(
        GlassWipeSimplePattern.OffsetRoundTrips,
        GlassWipePathInterpolation.CircularArc,
        roundTripStart,
        roundTripControl,
        roundTripEnd,
        1,
        120,
        120,
        3.5,
        maskGeometry: wide);
    Vector2Equal(roundTripStart / 100, GetSimplePosition(offset, 0),
        "ずらし往復3点円弧の始点が不正です。", 0.00001f);
    Vector2Equal(roundTripControl / 100, GetSimplePosition(offset, 30),
        "ずらし往復3点円弧の往路経由点が不正です。", 0.00001f);
    Vector2Equal(roundTripEnd / 100, GetSimplePosition(offset, 60),
        "ずらし往復3点円弧の折返し点が不正です。", 0.00001f);
    Vector2Equal(roundTripControl / 100 + new Vector2(0, 0.0175f), GetSimplePosition(offset, 90),
        "ずらし往復3点円弧の復路経由点が不正です。", 0.00001f);
    Vector2Equal(roundTripStart / 100 + new Vector2(0, 0.035f), GetSimplePosition(offset, 120),
        "ずらし往復3点円弧の復路終点が不正です。", 0.00001f);

    foreach (var roundTrips in Enumerable.Range(1, 5))
    {
        foreach (var returnOffset in new[] { 0d, 3.5d, 20d })
        {
            var itemLength = roundTrips * 120;
            var full = WipeSimplePathGenerator.Generate(
                GlassWipeSimplePattern.OffsetRoundTrips,
                GlassWipePathInterpolation.CircularArc,
                roundTripStart,
                roundTripControl,
                roundTripEnd,
                roundTrips,
                itemLength,
                itemLength,
                returnOffset,
                maskGeometry: wide);
            var same = WipeSimplePathGenerator.Generate(
                GlassWipeSimplePattern.OffsetRoundTrips,
                GlassWipePathInterpolation.CircularArc,
                roundTripStart,
                roundTripControl,
                roundTripEnd,
                roundTrips,
                itemLength,
                itemLength,
                returnOffset,
                maskGeometry: wide);
            AssertSimplePathResult(
                full,
                itemLength,
                itemLength,
                $"ずらし往復3点円弧の{roundTrips}往復・{returnOffset}%",
                WipeSimplePathGenerator.MinimumGeneratedPathCoordinate,
                WipeSimplePathGenerator.MaximumGeneratedPathCoordinate);
            True(full.Samples.SequenceEqual(same.Samples),
                "同じ3点円弧設定は決定論的なサンプル列を返す必要があります。");
            Equal(full.Signature, same.Signature,
                "同じ3点円弧設定は決定論的な署名を返す必要があります。");

            var expectedFinalY = Math.Clamp(
                roundTripStart.Y / 100 +
                    (float)(roundTrips * returnOffset / 100),
                WipeSimplePathGenerator.MinimumGeneratedPathCoordinate,
                WipeSimplePathGenerator.MaximumGeneratedPathCoordinate);
            Vector2Equal(
                new Vector2(roundTripStart.X / 100, expectedFinalY),
                GetSimplePosition(full, itemLength),
                "ずらし往復3点円弧の最終到達点が不正です。",
                0.00001f);

            foreach (var currentFrame in new[]
            {
                0,
                Math.Min(37, itemLength),
                itemLength / 2,
                itemLength,
            })
            {
                var direct = WipeSimplePathGenerator.Generate(
                    GlassWipeSimplePattern.OffsetRoundTrips,
                    GlassWipePathInterpolation.CircularArc,
                    roundTripStart,
                    roundTripControl,
                    roundTripEnd,
                    roundTrips,
                    currentFrame,
                    itemLength,
                    returnOffset,
                    maskGeometry: wide);
                True(direct.Samples.SequenceEqual(
                        full.Samples.Take(currentFrame + 1)),
                    "3点円弧の直接シーク結果は完全生成した軌跡のprefixと一致する必要があります。");
            }
        }
    }
}

static void VerifyThreePointCircularArcFallbackAndCompatibility()
{
    var square = WipeMaskGeometry.Create(1000, 1000);
    foreach (var input in new[]
    {
        (Start: new Vector2(10, 50), Control: new Vector2(50, 50), End: new Vector2(90, 50), Context: "共線"),
        (Start: new Vector2(20, 50), Control: new Vector2(20, 50), End: new Vector2(80, 50), Context: "重複点"),
        (Start: new Vector2(10, 10), Control: new Vector2(90, 10), End: new Vector2(10, 90), Context: "領域外へ出る弧"),
        (Start: new Vector2(float.NaN, 50), Control: new Vector2(float.PositiveInfinity, 50), End: new Vector2(80, 50), Context: "非有限値"),
    })
    {
        var circular = WipeSimplePathGenerator.Generate(
            GlassWipeSimplePattern.GentleArcOnce,
            GlassWipePathInterpolation.CircularArc,
            input.Start,
            input.Control,
            input.End,
            1,
            120,
            120,
            maskGeometry: square);
        var smooth = WipeSimplePathGenerator.Generate(
            GlassWipeSimplePattern.GentleArcOnce,
            GlassWipePathInterpolation.Smooth,
            input.Start,
            input.Control,
            input.End,
            1,
            120,
            120,
            maskGeometry: square);
        True(circular.Samples.SequenceEqual(smooth.Samples),
            $"3点円弧の{input.Context}では既存のなめらか補間へフォールバックする必要があります。");
        AssertSimplePathResult(circular, 120, 120, $"3点円弧の{input.Context}フォールバック");
    }

    var collinearStart = new Vector2(10, 50);
    var collinearControl = new Vector2(50, 50);
    var collinearEnd = new Vector2(90, 50);
    foreach (var pattern in new[]
    {
        GlassWipeSimplePattern.ArcRoundTrips,
        GlassWipeSimplePattern.OffsetRoundTrips,
    })
    {
        var circular = WipeSimplePathGenerator.Generate(
            pattern,
            GlassWipePathInterpolation.CircularArc,
            collinearStart,
            collinearControl,
            collinearEnd,
            2,
            240,
            240,
            maskGeometry: square);
        var smooth = WipeSimplePathGenerator.Generate(
            pattern,
            GlassWipePathInterpolation.Smooth,
            collinearStart,
            collinearControl,
            collinearEnd,
            2,
            240,
            240,
            maskGeometry: square);
        True(circular.Samples.SequenceEqual(smooth.Samples),
            $"{pattern}の退化した3点円弧は既存のなめらか補間と一致する必要があります。");
    }

    var partialFallbackCircular = WipeSimplePathGenerator.Generate(
        GlassWipeSimplePattern.OffsetRoundTrips,
        GlassWipePathInterpolation.CircularArc,
        new Vector2(20, 50),
        new Vector2(20, 60),
        new Vector2(80, 50),
        1,
        120,
        120,
        20,
        maskGeometry: square);
    var partialFallbackSmooth = WipeSimplePathGenerator.Generate(
        GlassWipeSimplePattern.OffsetRoundTrips,
        GlassWipePathInterpolation.Smooth,
        new Vector2(20, 50),
        new Vector2(20, 60),
        new Vector2(80, 50),
        1,
        120,
        120,
        20,
        maskGeometry: square);
    True(!partialFallbackCircular.Samples.Take(60).SequenceEqual(
            partialFallbackSmooth.Samples.Take(60)),
        "ずらし往復の有効な往路円弧は、なめらか補間と異なる必要があります。");
    True(partialFallbackCircular.Samples.Skip(60).SequenceEqual(
            partialFallbackSmooth.Samples.Skip(60)),
        "ずらし往復で復路だけ退化した場合は、その区間だけなめらか補間へ戻る必要があります。");

    var start = new Vector2(20, 70);
    var control = new Vector2(50, 20);
    var end = new Vector2(80, 70);
    var wide = WipeMaskGeometry.Create(1600, 900);
    var tall = WipeMaskGeometry.Create(900, 1600);
    foreach (var pattern in new[]
    {
        GlassWipeSimplePattern.StraightOnce,
        GlassWipeSimplePattern.ShortRoundTrips,
    })
    {
        var linear = WipeSimplePathGenerator.Generate(
            pattern,
            GlassWipePathInterpolation.Linear,
            start,
            control,
            end,
            2,
            120,
            120,
            maskGeometry: wide);
        var circular = WipeSimplePathGenerator.Generate(
            pattern,
            GlassWipePathInterpolation.CircularArc,
            start,
            control,
            end,
            2,
            120,
            120,
            maskGeometry: wide);
        True(linear.Samples.SequenceEqual(circular.Samples),
            $"{pattern}は3点円弧を選んでも経由点を使わず直線形状を維持する必要があります。");
    }

    foreach (var interpolation in new[]
    {
        GlassWipePathInterpolation.Linear,
        GlassWipePathInterpolation.Smooth,
    })
    {
        var wideResult = WipeSimplePathGenerator.Generate(
            GlassWipeSimplePattern.GentleArcOnce,
            interpolation,
            start,
            control,
            end,
            1,
            120,
            120,
            maskGeometry: wide);
        var tallResult = WipeSimplePathGenerator.Generate(
            GlassWipeSimplePattern.GentleArcOnce,
            interpolation,
            start,
            control,
            end,
            1,
            120,
            120,
            maskGeometry: tall);
        True(wideResult.Samples.SequenceEqual(tallResult.Samples),
            $"既存の{interpolation}補間は実領域geometryの影響を受けてはいけません。");
        Equal(wideResult.Signature, tallResult.Signature,
            $"既存の{interpolation}補間は実領域geometryを署名へ含めてはいけません。");
    }

    var circularWide = WipeSimplePathGenerator.Generate(
        GlassWipeSimplePattern.GentleArcOnce,
        GlassWipePathInterpolation.CircularArc,
        start,
        control,
        end,
        1,
        120,
        120,
        maskGeometry: wide);
    var circularTall = WipeSimplePathGenerator.Generate(
        GlassWipeSimplePattern.GentleArcOnce,
        GlassWipePathInterpolation.CircularArc,
        start,
        control,
        end,
        1,
        120,
        120,
        maskGeometry: tall);
    True(!circularWide.Samples.SequenceEqual(circularTall.Samples),
        "3点円弧は実領域geometryを生成サンプルへ反映する必要があります。");
    True(circularWide.Signature != circularTall.Signature,
        "3点円弧は実領域geometryを署名へ反映する必要があります。");
    var circularWideSame = WipeSimplePathGenerator.Generate(
        GlassWipeSimplePattern.GentleArcOnce,
        GlassWipePathInterpolation.CircularArc,
        start,
        control,
        end,
        1,
        120,
        120,
        maskGeometry: wide);
    var wideSnapshot = circularWide.CreateSnapshot(60);
    var wideSameSnapshot = circularWideSame.CreateSnapshot(60);
    var tallSnapshot = circularTall.CreateSnapshot(60);
    Equal(wideSnapshot.Fingerprint, wideSameSnapshot.Fingerprint,
        "同じ3点円弧とgeometryのFingerprintが一致しません。");
    Equal(WipeMaskUpdateKind.Reuse,
        WipeMaskUpdatePlan.Create(wideSnapshot, wide, wideSameSnapshot, wide).Kind,
        "同じ3点円弧とgeometryはMaskを再利用する必要があります。");
    True(wideSnapshot.Fingerprint != tallSnapshot.Fingerprint,
        "3点円弧のgeometry変更でFingerprintが変化する必要があります。");
    Equal(WipeMaskUpdateKind.Rebuild,
        WipeMaskUpdatePlan.Create(wideSnapshot, wide, tallSnapshot, tall).Kind,
        "3点円弧のgeometry変更後はMaskを再構築する必要があります。");

    var invalidGeometry = WipeSimplePathGenerator.Generate(
        GlassWipeSimplePattern.GentleArcOnce,
        GlassWipePathInterpolation.CircularArc,
        start,
        control,
        end,
        1,
        120,
        120,
        maskGeometry: new WipeMaskGeometry(float.NaN, 0));
    var unitGeometry = WipeSimplePathGenerator.Generate(
        GlassWipeSimplePattern.GentleArcOnce,
        GlassWipePathInterpolation.CircularArc,
        start,
        control,
        end,
        1,
        120,
        120,
        maskGeometry: square);
    True(invalidGeometry.Samples.SequenceEqual(unitGeometry.Samples),
        "不正な3点円弧geometryは正方形geometryへフォールバックする必要があります。");
    Equal(invalidGeometry.Signature, unitGeometry.Signature,
        "不正な3点円弧geometryは正規化後の署名を使用する必要があります。");
}

static void VerifySimplePresetInputApplicability()
{
    const int currentFrame = 119;
    const int itemLength = 120;
    var start = new Vector2(10, 80);
    var changedStart = new Vector2(25, 65);
    var control = new Vector2(45, 10);
    var changedControl = new Vector2(70, 85);
    var end = new Vector2(90, 30);
    var changedEnd = new Vector2(75, 45);

    foreach (var pattern in Enum.GetValues<GlassWipeSimplePattern>())
    {
        var linear = WipeSimplePathGenerator.Generate(
            pattern, GlassWipePathInterpolation.Linear,
            start, control, end, 2, currentFrame, itemLength, 3.5);
        var changedLinearStart = WipeSimplePathGenerator.Generate(
            pattern, GlassWipePathInterpolation.Linear,
            changedStart, control, end, 2, currentFrame, itemLength, 3.5);
        var changedLinearEnd = WipeSimplePathGenerator.Generate(
            pattern, GlassWipePathInterpolation.Linear,
            start, control, changedEnd, 2, currentFrame, itemLength, 3.5);
        var changedLinearControl = WipeSimplePathGenerator.Generate(
            pattern, GlassWipePathInterpolation.Linear,
            start, changedControl, end, 2, currentFrame, itemLength, 3.5);
        var smooth = WipeSimplePathGenerator.Generate(
            pattern, GlassWipePathInterpolation.Smooth,
            start, control, end, 2, currentFrame, itemLength, 3.5);
        var changedSmoothControl = WipeSimplePathGenerator.Generate(
            pattern, GlassWipePathInterpolation.Smooth,
            start, changedControl, end, 2, currentFrame, itemLength, 3.5);
        var changedRoundTrips = WipeSimplePathGenerator.Generate(
            pattern, GlassWipePathInterpolation.Linear,
            start, control, end, 4, currentFrame, itemLength, 3.5);
        var changedReturnOffset = WipeSimplePathGenerator.Generate(
            pattern, GlassWipePathInterpolation.Smooth,
            start, control, end, 2, currentFrame, itemLength, 15);

        True(!linear.Samples.SequenceEqual(changedLinearStart.Samples),
            $"{pattern}は開始座標の変更をサンプル列へ反映する必要があります。");
        True(!linear.Samples.SequenceEqual(changedLinearEnd.Samples),
            $"{pattern}は終了座標の変更をサンプル列へ反映する必要があります。");

        var linearControlAffectsSamples = pattern is
            GlassWipeSimplePattern.GentleArcOnce or
            GlassWipeSimplePattern.ArcRoundTrips;
        Equal(
            linearControlAffectsSamples,
            !linear.Samples.SequenceEqual(changedLinearControl.Samples),
            $"{pattern}の直線補間における経由点の適用範囲が不正です。");

        var smoothControlAffectsSamples = pattern is
            GlassWipeSimplePattern.GentleArcOnce or
            GlassWipeSimplePattern.ArcRoundTrips or
            GlassWipeSimplePattern.OffsetRoundTrips;
        Equal(
            smoothControlAffectsSamples,
            !smooth.Samples.SequenceEqual(changedSmoothControl.Samples),
            $"{pattern}のなめらか補間における経由点の適用範囲が不正です。");

        var interpolationAffectsSamples = pattern is
            GlassWipeSimplePattern.GentleArcOnce or
            GlassWipeSimplePattern.ArcRoundTrips or
            GlassWipeSimplePattern.OffsetRoundTrips;
        Equal(
            interpolationAffectsSamples,
            !linear.Samples.SequenceEqual(smooth.Samples),
            $"{pattern}の補間方式の適用範囲が不正です。");

        var roundTripsAffectSamples = pattern is
            GlassWipeSimplePattern.ShortRoundTrips or
            GlassWipeSimplePattern.ArcRoundTrips or
            GlassWipeSimplePattern.OffsetRoundTrips;
        Equal(
            roundTripsAffectSamples,
            !linear.Samples.SequenceEqual(changedRoundTrips.Samples),
            $"{pattern}の往復回数の適用範囲が不正です。");

        var returnOffsetAffectsSamples = pattern == GlassWipeSimplePattern.OffsetRoundTrips;
        Equal(
            returnOffsetAffectsSamples,
            !smooth.Samples.SequenceEqual(changedReturnOffset.Samples),
            $"{pattern}のずらし量の適用範囲が不正です。");
    }

    var offsetSmooth = WipeSimplePathGenerator.Generate(
        GlassWipeSimplePattern.OffsetRoundTrips, GlassWipePathInterpolation.Smooth,
        start, control, end, 2, currentFrame, itemLength, 3.5);
    var changedOffsetSmoothControl = WipeSimplePathGenerator.Generate(
        GlassWipeSimplePattern.OffsetRoundTrips, GlassWipePathInterpolation.Smooth,
        start, changedControl, end, 2, currentFrame, itemLength, 3.5);
    True(!offsetSmooth.Samples.SequenceEqual(changedOffsetSmoothControl.Samples),
        "ずらし往復のなめらか補間は経由点の変更をサンプル列へ反映する必要があります。");
}

static void VerifySimpleRoundTripPatterns()
{
    var start = new Vector2(10, 50);
    var control = new Vector2(50, 15);
    var end = new Vector2(90, 50);

    foreach (var pattern in new[]
    {
        GlassWipeSimplePattern.ShortRoundTrips,
        GlassWipeSimplePattern.ArcRoundTrips,
        GlassWipeSimplePattern.OffsetRoundTrips,
    })
    {
        WipeSimplePathGenerationResult? previous = null;
        for (var roundTrips = 1; roundTrips <= 5; roundTrips++)
        {
            var current = WipeSimplePathGenerator.Generate(
                pattern, GlassWipePathInterpolation.Linear, start, control, end, roundTrips, 120, 120);
            AssertSimplePathResult(current, 120, 120, $"{pattern}の{roundTrips}往復");

            if (previous is not null)
            {
                True(
                    previous.Signature != current.Signature &&
                    !previous.Samples.SequenceEqual(current.Samples),
                    $"{pattern}は往復回数を署名とサンプル列へ反映する必要があります。");
            }
            previous = current;
        }
    }

    var shortOnce = WipeSimplePathGenerator.Generate(
        GlassWipeSimplePattern.ShortRoundTrips, GlassWipePathInterpolation.Linear, start, control, end, 1, 120, 120);
    Vector2Equal(start / 100, new Vector2(shortOnce.Samples[0].X, shortOnce.Samples[0].Y), "短い往復の始点が不正です。");
    Vector2Equal(end / 100, new Vector2(shortOnce.Samples[60].X, shortOnce.Samples[60].Y), "短い往復の折返し点が不正です。");
    Vector2Equal(start / 100, new Vector2(shortOnce.Samples[^1].X, shortOnce.Samples[^1].Y), "1往復は始点へ戻る必要があります。");

    var arcOnce = WipeSimplePathGenerator.Generate(
        GlassWipeSimplePattern.ArcRoundTrips, GlassWipePathInterpolation.Linear, start, control, end, 1, 120, 120);
    Vector2Equal(end / 100, new Vector2(arcOnce.Samples[60].X, arcOnce.Samples[60].Y), "弧をなぞる往復の折返し点が不正です。");
    Vector2Equal(start / 100, new Vector2(arcOnce.Samples[^1].X, arcOnce.Samples[^1].Y), "弧をなぞる1往復は始点へ戻る必要があります。");

    var offsetOnce = WipeSimplePathGenerator.Generate(
        GlassWipeSimplePattern.OffsetRoundTrips, GlassWipePathInterpolation.Linear, start, control, end, 1, 120, 120);
    Vector2Equal(end / 100, new Vector2(offsetOnce.Samples[60].X, offsetOnce.Samples[60].Y), "ずらし往復の折返し点が不正です。");
    True(offsetOnce.Samples[^1].Y > offsetOnce.Samples[0].Y, "ずらし往復の復路は始点からオフセットされる必要があります。");
}

static void VerifySimpleWipeAmountPerPass()
{
    Equal(
        40d,
        WipeSimplePathGenerator.DefaultWipeAmountPerPassPercent,
        "1回あたりの拭き取り量の既定値は40%である必要があります。");

    var start = new Vector2(20, 50);
    var control = new Vector2(50, 20);
    var end = new Vector2(80, 50);
    foreach (var pattern in Enum.GetValues<GlassWipeSimplePattern>())
    {
        var defaultAmount = WipeSimplePathGenerator.Generate(
            pattern, GlassWipePathInterpolation.Smooth,
            start, control, end, 2, 120, 120, 3.5, 40);
        var changedAmount = WipeSimplePathGenerator.Generate(
            pattern, GlassWipePathInterpolation.Smooth,
            start, control, end, 2, 120, 120, 3.5, 75);
        var isRoundTrip = pattern is
            GlassWipeSimplePattern.ShortRoundTrips or
            GlassWipeSimplePattern.ArcRoundTrips or
            GlassWipeSimplePattern.OffsetRoundTrips;

        Equal(
            isRoundTrip,
            !defaultAmount.Samples.SequenceEqual(changedAmount.Samples),
            $"{pattern}の1回あたりの拭き取り量の適用範囲が不正です。");
        Equal(
            isRoundTrip,
            defaultAmount.Signature != changedAmount.Signature,
            $"{pattern}の1回あたりの拭き取り量の署名適用範囲が不正です。");
        Equal(
            isRoundTrip
                ? WipeMaskAccumulationMode.PerPassMaximum
                : WipeMaskAccumulationMode.SourceOver,
            defaultAmount.Style.AccumulationMode,
            $"{pattern}のマスク累積方式が不正です。");
    }

    var zero = WipeSimplePathGenerator.Generate(
        GlassWipeSimplePattern.ShortRoundTrips,
        GlassWipePathInterpolation.Linear,
        start, control, end, 2, 120, 120, 3.5, 0);
    var maximum = WipeSimplePathGenerator.Generate(
        GlassWipeSimplePattern.ShortRoundTrips,
        GlassWipePathInterpolation.Linear,
        start, control, end, 2, 120, 120, 3.5, 100);
    var lower = WipeSimplePathGenerator.Generate(
        GlassWipeSimplePattern.ShortRoundTrips,
        GlassWipePathInterpolation.Linear,
        start, control, end, 2, 120, 120, 3.5, -1);
    var upper = WipeSimplePathGenerator.Generate(
        GlassWipeSimplePattern.ShortRoundTrips,
        GlassWipePathInterpolation.Linear,
        start, control, end, 2, 120, 120, 3.5, 101);
    var nonFinite = WipeSimplePathGenerator.Generate(
        GlassWipeSimplePattern.ShortRoundTrips,
        GlassWipePathInterpolation.Linear,
        start, control, end, 2, 120, 120, 3.5, double.NaN);
    var defaultAmountResult = WipeSimplePathGenerator.Generate(
        GlassWipeSimplePattern.ShortRoundTrips,
        GlassWipePathInterpolation.Linear,
        start, control, end, 2, 120, 120);

    True(zero.Samples.All(sample => sample.Strength == 0), "拭き取り量0%をサンプルへ反映する必要があります。");
    True(maximum.Samples.All(sample => sample.Strength == 1), "拭き取り量100%をサンプルへ反映する必要があります。");
    True(zero.Samples.SequenceEqual(lower.Samples), "負の拭き取り量は0%へ補正する必要があります。");
    True(maximum.Samples.SequenceEqual(upper.Samples), "100%を超える拭き取り量は100%へ補正する必要があります。");
    True(defaultAmountResult.Samples.SequenceEqual(nonFinite.Samples), "非有限の拭き取り量は40%へ補正する必要があります。");
    Equal(defaultAmountResult.Signature, nonFinite.Signature, "非有限の拭き取り量は40%の署名へ補正する必要があります。");

    var geometry = WipeMaskGeometry.Create(1920, 1080);
    True(
        zero.CreateSnapshot(60).Fingerprint != maximum.CreateSnapshot(60).Fingerprint,
        "拭き取り量の変更でFingerprintが変化する必要があります。");
    Equal(
        WipeMaskUpdateKind.Rebuild,
        WipeMaskUpdatePlan.Create(
            zero.CreateSnapshot(60),
            geometry,
            maximum.CreateSnapshot(60),
            geometry).Kind,
        "拭き取り量を変更した後はMaskを再構築する必要があります。");
}

static void VerifyPerPassMaskAccumulation()
{
    const int itemLength = 120;
    const float wipeAmount = 0.4f;
    var point = new Vector2(50, 50);
    var geometry = WipeMaskGeometry.Create(1920, 1080);
    var probe = new[] { point / 100 };

    for (var passCount = 1; passCount <= 4; passCount++)
    {
        var frame = passCount * itemLength / 4;
        var result = WipeSimplePathGenerator.Generate(
            GlassWipeSimplePattern.ShortRoundTrips,
            GlassWipePathInterpolation.Linear,
            point,
            point,
            point,
            2,
            frame,
            itemLength,
            WipeSimplePathGenerator.DefaultReturnOffsetPercent,
            wipeAmount * 100);
        var stamps = WipeBrushStampGenerator.Generate(
            result.Samples,
            0,
            geometry,
            result.Style);
        var actual = ApplyStampsPerPassCoverageUnion(
            new float[probe.Length],
            probe,
            stamps)[0];
        var expected = 1 - MathF.Pow(1 - wipeAmount, passCount);

        NearlyEqual(expected, actual, $"{passCount}回通過後の累積拭き取り率", 0.0001f);
        Equal(
            passCount,
            stamps.Select(stamp => stamp.AccumulationGroup).Distinct().Count(),
            $"{passCount}回通過後の片道グループ数が不正です。");
        NearlyEqual(
            wipeAmount,
            WipeMaskRenderer.ResolvePassOpacity(
                stamps,
                0,
                stamps.Count),
            $"{passCount}回通過時の片道強度",
            0.0001f);
    }

    for (var roundTrips = 1; roundTrips <= 5; roundTrips++)
    {
        var result = WipeSimplePathGenerator.Generate(
            GlassWipeSimplePattern.ArcRoundTrips,
            GlassWipePathInterpolation.Smooth,
            new Vector2(10, 50),
            new Vector2(50, 20),
            new Vector2(90, 50),
            roundTrips,
            itemLength,
            itemLength,
            WipeSimplePathGenerator.DefaultReturnOffsetPercent,
            40);
        Equal(0, result.Samples[0].AccumulationGroup, $"{roundTrips}往復の最初の片道番号が不正です。");
        Equal(roundTrips * 2 - 1, result.Samples[^1].AccumulationGroup, $"{roundTrips}往復の最後の片道番号が不正です。");
        True(
            result.Samples.Zip(result.Samples.Skip(1))
                .All(pair => pair.First.AccumulationGroup <= pair.Second.AccumulationGroup),
            $"{roundTrips}往復の片道番号は単調に増加する必要があります。");
    }

    NearlyEqual(0.994f, 1 - MathF.Pow(1 - wipeAmount, 10), "40%で10回通過後の累積拭き取り率", 0.0001f);
}

static void VerifyPerPassCoverageStrengthSeparation()
{
    const float coverage = 0.35f;
    const float wipeAmount = 0.4f;
    var stamps = new[]
    {
        new WipeBrushStamp(default, 1, 1, default, wipeAmount, 0, 0),
        new WipeBrushStamp(default, 1, 1, default, wipeAmount, 0, 0),
    };
    var passOpacity = WipeMaskRenderer.ResolvePassOpacity(
        stamps,
        0,
        stamps.Length);
    var normalizedOpacity = WipeMaskRenderer.ScaleStampOpacity(
        stamps[0].Alpha,
        1 / passOpacity);
    var onePassCoverage = coverage + coverage * (1 - coverage);
    var normalizedStampCoverage = coverage * normalizedOpacity;
    var roundTripPassCoverage = normalizedStampCoverage +
        normalizedStampCoverage * (1 - normalizedStampCoverage);

    NearlyEqual(wipeAmount, passOpacity, "片道強度を形状カバー率から分離する", 0.0001f);
    NearlyEqual(1, normalizedOpacity, "片道マスクではスタンプ強度を正規化する", 0.0001f);
    NearlyEqual(
        onePassCoverage,
        roundTripPassCoverage,
        "100%では一回拭きと同じカバー率になる",
        0.0001f);
    NearlyEqual(
        onePassCoverage * wipeAmount,
        roundTripPassCoverage * passOpacity,
        "40%では合成済みカバー率へ片道強度を一度だけ掛ける",
        0.0001f);
    Equal(
        false,
        WipeMaskRenderer.UsesMaximumPassCoverage(GlassWipeBrushShape.Hand),
        "手形は一回拭きと同じ太さを保つ合成方式である必要があります。");
    Equal(
        false,
        WipeMaskRenderer.UsesMaximumPassCoverage(GlassWipeBrushShape.ShoePrint),
        "靴跡は一回拭きと同じ太さを保つ合成方式である必要があります。");
    Equal(0f, WipeMaskRenderer.ScaleStampOpacity(float.NaN, 1), "非有限のスタンプ強度は0へ補正する");
    Equal(0f, WipeMaskRenderer.ScaleStampOpacity(1, float.PositiveInfinity), "非有限の倍率は0へ補正する");
    Equal(
        0f,
        WipeMaskRenderer.ResolvePassOpacity(
            [new WipeBrushStamp(default, 1, 1, default, float.NaN, 0, 0)],
            0,
            1),
        "非有限の片道強度は無視する");
}

static void VerifyPerPassEndpointCoverageUniformity()
{
    const float softEdgeCoverage = 0.35f;
    const float wipeAmount = 0.4f;
    var endpointCoverage = softEdgeCoverage;
    var interiorCoverage = MathF.Max(
        softEdgeCoverage,
        softEdgeCoverage);

    foreach (var shape in new[]
             {
                 GlassWipeBrushShape.Ellipse,
                 GlassWipeBrushShape.Rectangle,
             })
    {
        Equal(
            true,
            WipeMaskRenderer.UsesMaximumPassCoverage(shape),
            $"{shape}の片道内は重なり回数に依存しない必要があります。");
        NearlyEqual(
            endpointCoverage * wipeAmount,
            interiorCoverage * wipeAmount,
            $"{shape}の端部と中央は同じ拭き取り強度である必要があります。",
            0.0001f);
    }
}

static void VerifyRoundTripBoundaryCoverage()
{
    const int itemLength = 120;
    var start = new Vector2(15, 65);
    var control = new Vector2(50, 25);
    var end = new Vector2(85, 65);
    var geometry = WipeMaskGeometry.Create(1920, 1080);
    var expectedGroups = Enumerable.Range(0, 4).ToArray();

    foreach (var (pattern, interpolation) in new[]
             {
                 (
                     GlassWipeSimplePattern.ShortRoundTrips,
                     GlassWipePathInterpolation.Linear),
                 (
                     GlassWipeSimplePattern.ArcRoundTrips,
                     GlassWipePathInterpolation.CircularArc),
             })
    {
        var result = WipeSimplePathGenerator.Generate(
            pattern,
            interpolation,
            start,
            control,
            end,
            2,
            itemLength,
            itemLength,
            WipeSimplePathGenerator.DefaultReturnOffsetPercent,
            40,
            geometry);
        var stamps = WipeBrushStampGenerator.Generate(
            result.Samples,
            0,
            geometry,
            result.Style);

        foreach (var (name, point) in new[]
                 {
                     ("始点", start),
                     ("終点", end),
                 })
        {
            var center = point / 100 * WipeMaskRenderer.MaskSize;
            var actualGroups = stamps
                .Where(stamp =>
                    Vector2.DistanceSquared(stamp.Center, center) <= 0.01f)
                .Select(stamp => stamp.AccumulationGroup)
                .Distinct()
                .Order()
                .ToArray();
            True(
                actualGroups.SequenceEqual(expectedGroups),
                $"{pattern}の{name}は全片道で共有される必要があります。");
        }
    }
}

static void VerifyOffsetRoundTripSmoothInterpolation()
{
    const int itemLength = 120;
    var start = new Vector2(10, 50);
    var control = new Vector2(50, 15);
    var changedControl = new Vector2(50, 85);
    var end = new Vector2(90, 50);
    var linear = WipeSimplePathGenerator.Generate(
        GlassWipeSimplePattern.OffsetRoundTrips,
        GlassWipePathInterpolation.Linear,
        start,
        control,
        end,
        1,
        itemLength,
        itemLength);
    var smooth = WipeSimplePathGenerator.Generate(
        GlassWipeSimplePattern.OffsetRoundTrips,
        GlassWipePathInterpolation.Smooth,
        start,
        control,
        end,
        1,
        itemLength,
        itemLength);
    var changed = WipeSimplePathGenerator.Generate(
        GlassWipeSimplePattern.OffsetRoundTrips,
        GlassWipePathInterpolation.Smooth,
        start,
        changedControl,
        end,
        1,
        itemLength,
        itemLength);

    AssertSimplePathResult(smooth, itemLength, itemLength, "ずらし往復のなめらか補間");
    True(!linear.Samples.SequenceEqual(smooth.Samples), "ずらし往復では直線補間となめらか補間が異なる必要があります。");
    True(!smooth.Samples.SequenceEqual(changed.Samples), "ずらし往復のなめらか補間は経由点の変更をサンプル列へ反映する必要があります。");
    Vector2Equal(start / 100, new Vector2(smooth.Samples[0].X, smooth.Samples[0].Y), "ずらし往復のなめらか補間の始点が不正です。");
    Vector2Equal(control / 100, new Vector2(smooth.Samples[30].X, smooth.Samples[30].Y), "ずらし往復のなめらか補間が経由点を通っていません。");
    Vector2Equal(end / 100, new Vector2(smooth.Samples[60].X, smooth.Samples[60].Y), "ずらし往復のなめらか補間の折返し点が不正です。");
    Vector2Equal(
        new Vector2(linear.Samples[^1].X, linear.Samples[^1].Y),
        new Vector2(smooth.Samples[^1].X, smooth.Samples[^1].Y),
        "ずらし往復のなめらか補間は既存の復路終点を維持する必要があります。");

    for (var roundTrips = 1; roundTrips <= 5; roundTrips++)
    {
        var currentLinear = WipeSimplePathGenerator.Generate(
            GlassWipeSimplePattern.OffsetRoundTrips,
            GlassWipePathInterpolation.Linear,
            start,
            control,
            end,
            roundTrips,
            itemLength,
            itemLength);
        var currentSmooth = WipeSimplePathGenerator.Generate(
            GlassWipeSimplePattern.OffsetRoundTrips,
            GlassWipePathInterpolation.Smooth,
            start,
            control,
            end,
            roundTrips,
            itemLength,
            itemLength);
        AssertSimplePathResult(
            currentSmooth,
            itemLength,
            itemLength,
            $"ずらし往復のなめらか補間の{roundTrips}往復");
        True(
            !currentLinear.Samples.SequenceEqual(currentSmooth.Samples),
            $"ずらし往復の{roundTrips}往復で補間方式をサンプル列へ反映する必要があります。");

        var segmentCount = roundTrips * 2;
        for (var nodeIndex = 0; nodeIndex <= segmentCount; nodeIndex++)
        {
            var nodeFrame = nodeIndex * itemLength / segmentCount;
            Vector2Equal(
                new Vector2(currentLinear.Samples[nodeFrame].X, currentLinear.Samples[nodeFrame].Y),
                new Vector2(currentSmooth.Samples[nodeFrame].X, currentSmooth.Samples[nodeFrame].Y),
                $"ずらし往復の{roundTrips}往復で既存ノードの到達時刻が変わっています。");
        }
    }

    var clamped = WipeSimplePathGenerator.Generate(
        GlassWipeSimplePattern.OffsetRoundTrips,
        GlassWipePathInterpolation.Smooth,
        new Vector2(-50, 150),
        new Vector2(50, 150),
        new Vector2(150, -50),
        5,
        itemLength,
        itemLength);
    AssertSimplePathResult(
        clamped,
        itemLength,
        itemLength,
        "入力Clamp端のずらし往復なめらか補間",
        WipeSimplePathGenerator.MinimumGeneratedPathCoordinate,
        WipeSimplePathGenerator.MaximumGeneratedPathCoordinate);
}

static void VerifyOffsetRoundTripAmount()
{
    const int itemLength = 120;
    Equal(
        3.5d,
        WipeSimplePathGenerator.DefaultReturnOffsetPercent,
        "ずれYの既定値は従来の3.5%である必要があります。");
    Equal(
        0d,
        WipeSimplePathGenerator.DefaultReturnOffsetXPercent,
        "ずれXの既定値は0%である必要があります。");

    var start = new Vector2(10, 50);
    var control = new Vector2(50, 15);
    var end = new Vector2(90, 50);
    var implicitDefault = WipeSimplePathGenerator.Generate(
        GlassWipeSimplePattern.OffsetRoundTrips,
        GlassWipePathInterpolation.Smooth,
        start,
        control,
        end,
        1,
        itemLength,
        itemLength);
    var explicitDefault = WipeSimplePathGenerator.Generate(
        GlassWipeSimplePattern.OffsetRoundTrips,
        GlassWipePathInterpolation.Smooth,
        start,
        control,
        end,
        1,
        itemLength,
        itemLength,
        WipeSimplePathGenerator.DefaultReturnOffsetPercent);
    var zero = WipeSimplePathGenerator.Generate(
        GlassWipeSimplePattern.OffsetRoundTrips,
        GlassWipePathInterpolation.Smooth,
        start,
        control,
        end,
        1,
        itemLength,
        itemLength,
        0);
    var maximum = WipeSimplePathGenerator.Generate(
        GlassWipeSimplePattern.OffsetRoundTrips,
        GlassWipePathInterpolation.Smooth,
        start,
        control,
        end,
        1,
        itemLength,
        itemLength,
        20);
    var minimum = WipeSimplePathGenerator.Generate(
        GlassWipeSimplePattern.OffsetRoundTrips,
        GlassWipePathInterpolation.Smooth,
        start,
        control,
        end,
        1,
        itemLength,
        itemLength,
        -20);
    var lower = WipeSimplePathGenerator.Generate(
        GlassWipeSimplePattern.OffsetRoundTrips,
        GlassWipePathInterpolation.Smooth,
        start,
        control,
        end,
        1,
        itemLength,
        itemLength,
        -99);
    var upper = WipeSimplePathGenerator.Generate(
        GlassWipeSimplePattern.OffsetRoundTrips,
        GlassWipePathInterpolation.Smooth,
        start,
        control,
        end,
        1,
        itemLength,
        itemLength,
        99);
    var nonFinite = WipeSimplePathGenerator.Generate(
        GlassWipeSimplePattern.OffsetRoundTrips,
        GlassWipePathInterpolation.Smooth,
        start,
        control,
        end,
        1,
        itemLength,
        itemLength,
        double.NaN);
    var right = WipeSimplePathGenerator.Generate(
        GlassWipeSimplePattern.OffsetRoundTrips,
        GlassWipePathInterpolation.Smooth,
        start,
        control,
        end,
        1,
        itemLength,
        itemLength,
        returnOffsetPercent: WipeSimplePathGenerator.DefaultReturnOffsetPercent,
        returnOffsetXPercent: 20);
    var left = WipeSimplePathGenerator.Generate(
        GlassWipeSimplePattern.OffsetRoundTrips,
        GlassWipePathInterpolation.Smooth,
        start,
        control,
        end,
        1,
        itemLength,
        itemLength,
        returnOffsetPercent: WipeSimplePathGenerator.DefaultReturnOffsetPercent,
        returnOffsetXPercent: -20);
    var xUpper = WipeSimplePathGenerator.Generate(
        GlassWipeSimplePattern.OffsetRoundTrips,
        GlassWipePathInterpolation.Smooth,
        start,
        control,
        end,
        1,
        itemLength,
        itemLength,
        returnOffsetPercent: WipeSimplePathGenerator.DefaultReturnOffsetPercent,
        returnOffsetXPercent: 99);
    var xNonFinite = WipeSimplePathGenerator.Generate(
        GlassWipeSimplePattern.OffsetRoundTrips,
        GlassWipePathInterpolation.Smooth,
        start,
        control,
        end,
        1,
        itemLength,
        itemLength,
        returnOffsetPercent: WipeSimplePathGenerator.DefaultReturnOffsetPercent,
        returnOffsetXPercent: double.NaN);

    True(implicitDefault.Samples.SequenceEqual(explicitDefault.Samples), "ずらし量の省略時は従来の3.5%サンプル列を維持する必要があります。");
    Equal(implicitDefault.Signature, explicitDefault.Signature, "ずらし量の省略時と3.5%明示時の署名が一致する必要があります。");
    True(!zero.Samples.SequenceEqual(implicitDefault.Samples), "ずらし量0%をサンプル列へ反映する必要があります。");
    True(!maximum.Samples.SequenceEqual(implicitDefault.Samples), "ずらし量20%をサンプル列へ反映する必要があります。");
    True(minimum.Samples.SequenceEqual(lower.Samples), "-20%未満のずれYは-20%へ補正する必要があります。");
    True(!zero.Samples.SequenceEqual(minimum.Samples), "負のずれYをサンプル列へ反映する必要があります。");
    True(maximum.Samples.SequenceEqual(upper.Samples), "20%を超えるずらし量は20%へ補正する必要があります。");
    True(implicitDefault.Samples.SequenceEqual(nonFinite.Samples), "非有限のずらし量は3.5%へ補正する必要があります。");
    Equal(implicitDefault.Signature, nonFinite.Signature, "非有限のずらし量は3.5%の署名へ補正する必要があります。");
    NearlyEqual(start.Y / 100, zero.Samples[^1].Y, "ずらし量0%の復路終点が不正です。");
    NearlyEqual(0.535f, implicitDefault.Samples[^1].Y, "ずらし量3.5%の復路終点が不正です。");
    NearlyEqual(0.7f, maximum.Samples[^1].Y, "ずらし量20%の復路終点が不正です。");
    NearlyEqual(0.3f, minimum.Samples[^1].Y, "ずれY -20%の復路終点が不正です。");
    NearlyEqual(0.3f, right.Samples[^1].X, "ずれX 20%の復路終点が不正です。");
    NearlyEqual(-0.1f, left.Samples[^1].X, "ずれX -20%は領域左端で停止せず画面外へ進む必要があります。");
    True(right.Samples.SequenceEqual(xUpper.Samples), "20%を超えるずれXは20%へ補正する必要があります。");
    NearlyEqual(start.X / 100, xNonFinite.Samples[^1].X, "非有限のずれXは0%へ補正する必要があります。");

    for (var roundTrips = 1; roundTrips <= 5; roundTrips++)
    {
        var current = WipeSimplePathGenerator.Generate(
            GlassWipeSimplePattern.OffsetRoundTrips,
            GlassWipePathInterpolation.Smooth,
            start,
            control,
            end,
            roundTrips,
            itemLength,
            itemLength,
            20);
        AssertSimplePathResult(
            current,
            itemLength,
            itemLength,
            $"ずらし量20%の{roundTrips}往復",
            WipeSimplePathGenerator.MinimumGeneratedPathCoordinate,
            WipeSimplePathGenerator.MaximumGeneratedPathCoordinate);
    }

    var arcZero = WipeSimplePathGenerator.Generate(
        GlassWipeSimplePattern.ArcRoundTrips,
        GlassWipePathInterpolation.Smooth,
        start,
        control,
        end,
        2,
        itemLength,
        itemLength,
        0);
    var arcMaximum = WipeSimplePathGenerator.Generate(
        GlassWipeSimplePattern.ArcRoundTrips,
        GlassWipePathInterpolation.Smooth,
        start,
        control,
        end,
        2,
        itemLength,
        itemLength,
        20);
    True(arcZero.Samples.SequenceEqual(arcMaximum.Samples), "ずらし量は他のプリセットへ影響してはいけません。");
    Equal(arcZero.Signature, arcMaximum.Signature, "ずらし量は他のプリセットの署名へ影響してはいけません。");

    var zeroSnapshot = zero.CreateSnapshot(60);
    var maximumSnapshot = maximum.CreateSnapshot(60);
    var geometry = WipeMaskGeometry.Create(1920, 1080);
    True(zeroSnapshot.Fingerprint != maximumSnapshot.Fingerprint, "ずらし量の変更でFingerprintが変化する必要があります。");
    Equal(
        WipeMaskUpdateKind.Rebuild,
        WipeMaskUpdatePlan.Create(zeroSnapshot, geometry, maximumSnapshot, geometry).Kind,
        "ずらし量を変更した後はMaskを再構築する必要があります。");
}

static void VerifyOffsetRoundTripOffscreenClipping()
{
    const int itemLength = 120;
    const int roundTrips = 5;
    const float offsetX = 0.1f;
    const float offsetY = -0.1f;
    var start = new Vector2(10, 40);
    var control = new Vector2(70, 80);
    var end = new Vector2(100, 60);
    var geometry = WipeMaskGeometry.Create(1920, 1080);

    var results = new Dictionary<GlassWipePathInterpolation, WipeSimplePathGenerationResult>();
    foreach (var interpolation in new[]
    {
        GlassWipePathInterpolation.Linear,
        GlassWipePathInterpolation.Smooth,
        GlassWipePathInterpolation.CircularArc,
    })
    {
        var result = WipeSimplePathGenerator.Generate(
            GlassWipeSimplePattern.OffsetRoundTrips,
            interpolation,
            start,
            control,
            end,
            roundTrips,
            itemLength,
            itemLength,
            returnOffsetPercent: offsetY * 100,
            maskGeometry: geometry,
            returnOffsetXPercent: offsetX * 100);
        AssertSimplePathResult(
            result,
            itemLength,
            itemLength,
            $"画面外へ進むずらし往復の{interpolation}",
            WipeSimplePathGenerator.MinimumGeneratedPathCoordinate,
            WipeSimplePathGenerator.MaximumGeneratedPathCoordinate);
        results.Add(interpolation, result);
    }

    var linear = results[GlassWipePathInterpolation.Linear];
    var smooth = results[GlassWipePathInterpolation.Smooth];
    var circular = results[GlassWipePathInterpolation.CircularArc];
    True(!linear.Samples.SequenceEqual(smooth.Samples),
        "画面外を含むずらし往復でも直線となめらか補間が異なる必要があります。");
    True(!smooth.Samples.SequenceEqual(circular.Samples),
        "画面外を含むずらし往復でもなめらか補間と3点円弧が異なる必要があります。");

    var segmentCount = roundTrips * 2;
    foreach (var (interpolation, result) in results)
    {
        for (var nodeIndex = 0; nodeIndex <= segmentCount; nodeIndex++)
        {
            var nodeFrame = nodeIndex * itemLength / segmentCount;
            Vector2 expected;
            if (nodeIndex == 0)
            {
                expected = start / 100;
            }
            else if (nodeIndex % 2 == 1)
            {
                var trip = nodeIndex / 2;
                expected = end / 100 + new Vector2(offsetX, offsetY) * trip;
            }
            else
            {
                var completedTrips = nodeIndex / 2;
                expected = start / 100 + new Vector2(offsetX, offsetY) * completedTrips;
            }

            Vector2Equal(
                expected,
                GetSimplePosition(result, nodeFrame),
                $"{interpolation}の第{nodeIndex}ノードが予定時刻・予定位置へ到達していません。",
                0.00001f);
        }
    }

    True(GetSimplePosition(linear, 36).X > 1,
        "2往復目以降の右端は100%へ固定せず画面外へ進む必要があります。");
    Vector2Equal(new Vector2(0.6f, -0.1f), GetSimplePosition(linear, itemLength),
        "5往復後の復路終点はずれX・ずれYを累積した画面外座標である必要があります。");

    var quad = new GlassWipeQuad(
        new Vector2(0.16f, 0.12f),
        new Vector2(1f, -0.85f),
        new Vector2(1f, 2f),
        new Vector2(0.16f, 0.94f));
    True(quad.TryCreateMapping(out var mapping), "再現条件の画面外Quadは有効である必要があります。");
    foreach (var sample in circular.Samples)
    {
        var mapped = mapping.LocalToInput.Transform(new Vector2(sample.X, sample.Y));
        True(float.IsFinite(mapped.X) && float.IsFinite(mapped.Y),
            "画面外のずらし往復をQuadへ写像しても非有限値を生成してはいけません。");
    }

    foreach (var xSign in new[] { -1, 1 })
    {
        foreach (var ySign in new[] { -1, 1 })
        {
            for (var trips = 1; trips <= 5; trips++)
            {
                var bounded = WipeSimplePathGenerator.Generate(
                    GlassWipeSimplePattern.OffsetRoundTrips,
                    GlassWipePathInterpolation.CircularArc,
                    new Vector2(50),
                    new Vector2(70, 30),
                    new Vector2(60, 50),
                    trips,
                    itemLength,
                    itemLength,
                    returnOffsetPercent: ySign * 20,
                    maskGeometry: geometry,
                    returnOffsetXPercent: xSign * 20);
                AssertSimplePathResult(
                    bounded,
                    itemLength,
                    itemLength,
                    $"画面外安全範囲のX{xSign}・Y{ySign}・{trips}往復",
                    WipeSimplePathGenerator.MinimumGeneratedPathCoordinate,
                    WipeSimplePathGenerator.MaximumGeneratedPathCoordinate);
            }
        }
    }
}

static void VerifySimplePathSanitization()
{
    var invalid = WipeSimplePathGenerator.Generate(
        (GlassWipeSimplePattern)99, (GlassWipePathInterpolation)99,
        new Vector2(float.NaN, float.PositiveInfinity),
        new Vector2(float.NegativeInfinity, float.NaN),
        new Vector2(float.PositiveInfinity, float.NegativeInfinity),
        99, int.MaxValue, -1);
    AssertSimplePathResult(invalid, 0, 0, "異常入力の補正結果");
    Vector2Equal(Vector2.Zero, new Vector2(invalid.Samples[0].X, invalid.Samples[0].Y), "非有限座標は安全な原点へ補正する必要があります。");

    var start = new Vector2(10, 50);
    var control = new Vector2(50, 20);
    var end = new Vector2(90, 50);
    var lower = WipeSimplePathGenerator.Generate(GlassWipeSimplePattern.ShortRoundTrips, GlassWipePathInterpolation.Linear, start, control, end, 0, 120, 120);
    var minimum = WipeSimplePathGenerator.Generate(GlassWipeSimplePattern.ShortRoundTrips, GlassWipePathInterpolation.Linear, start, control, end, 1, 120, 120);
    var upper = WipeSimplePathGenerator.Generate(GlassWipeSimplePattern.ShortRoundTrips, GlassWipePathInterpolation.Linear, start, control, end, 99, 120, 120);
    var maximum = WipeSimplePathGenerator.Generate(GlassWipeSimplePattern.ShortRoundTrips, GlassWipePathInterpolation.Linear, start, control, end, 5, 120, 120);
    True(minimum.Samples.SequenceEqual(lower.Samples), "往復回数0は1へ補正する必要があります。");
    True(maximum.Samples.SequenceEqual(upper.Samples), "往復回数5超過は5へ補正する必要があります。");
}

static void VerifySimplePathFingerprintAndUpdatePlan()
{
    var start = new Vector2(10, 50);
    var control = new Vector2(50, 15);
    var end = new Vector2(90, 50);
    var first = WipeSimplePathGenerator.Generate(
        GlassWipeSimplePattern.ArcRoundTrips, GlassWipePathInterpolation.Smooth,
        start, control, end, 2, 90, 120).CreateSnapshot(60);
    var same = WipeSimplePathGenerator.Generate(
        GlassWipeSimplePattern.ArcRoundTrips, GlassWipePathInterpolation.Smooth,
        start, control, end, 2, 90, 120).CreateSnapshot(60);
    var changed = WipeSimplePathGenerator.Generate(
        GlassWipeSimplePattern.ArcRoundTrips, GlassWipePathInterpolation.Smooth,
        start, control, new Vector2(85, 45), 2, 90, 120).CreateSnapshot(60);
    var future = WipeSimplePathGenerator.Generate(
        GlassWipeSimplePattern.ArcRoundTrips, GlassWipePathInterpolation.Smooth,
        start, control, end, 2, 91, 120).CreateSnapshot(60);
    var geometry = WipeMaskGeometry.Create(1920, 1080);

    Equal(WipePathInputMode.StrokeCollection, first.InputMode, "かんたん作成は安全な完全再構築規則を使う入力方式である必要があります。");
    Equal(first.Fingerprint, same.Fingerprint, "同じかんたん作成設定のFingerprintが一致しません。");
    True(first.Fingerprint != changed.Fingerprint, "かんたん作成設定の変更でFingerprintが変化する必要があります。");
    Equal(WipeMaskUpdateKind.Reuse, WipeMaskUpdatePlan.Create(first, geometry, same, geometry).Kind, "同じかんたん作成結果はMaskを再利用する必要があります。");
    Equal(WipeMaskUpdateKind.Rebuild, WipeMaskUpdatePlan.Create(first, geometry, changed, geometry).Kind, "かんたん作成設定を変更した後はMaskを再構築する必要があります。");
    Equal(WipeMaskUpdateKind.Rebuild, WipeMaskUpdatePlan.Create(first, geometry, future, geometry).Kind, "かんたん作成の前進フレームは完全な軌跡としてMaskを再構築する必要があります。");
}

static void AssertSimplePathResult(
    WipeSimplePathGenerationResult result,
    int expectedFrame,
    int expectedItemLength,
    string context,
    float minimumCoordinate = 0,
    float maximumCoordinate = 1)
{
    Equal(expectedFrame, result.Frame, $"{context}の現在フレームが不正です。");
    Equal(expectedItemLength, result.ItemLength, $"{context}のアイテム長が不正です。");
    Equal(expectedFrame + 1, result.Samples.Length, $"{context}のサンプル数が不正です。");
    for (var index = 0; index < result.Samples.Length; index++)
    {
        var sample = result.Samples[index];
        Equal(index, sample.Frame, $"{context}のフレーム順序が不正です。");
        True(float.IsFinite(sample.X) && float.IsFinite(sample.Y) &&
             float.IsFinite(sample.Contact) && float.IsFinite(sample.Size) &&
             float.IsFinite(sample.Strength) && float.IsFinite(sample.AspectRatio) &&
             float.IsFinite(sample.RotationRadians),
             $"{context}に非有限値を含めてはいけません。");
        True(
            sample.X >= minimumCoordinate && sample.X <= maximumCoordinate &&
            sample.Y >= minimumCoordinate && sample.Y <= maximumCoordinate,
            $"{context}の座標は{minimumCoordinate}から{maximumCoordinate}へ収まる必要があります。");
    }
}

static Vector2 GetSimplePosition(
    WipeSimplePathGenerationResult result,
    int frame)
{
    var sample = result.Samples[frame];
    return new Vector2(sample.X, sample.Y);
}

static string GetDisplayName<TEnum>(TEnum value) where TEnum : struct, Enum
{
    var field = typeof(TEnum).GetField(value.ToString())
        ?? throw new InvalidOperationException($"{typeof(TEnum).Name}のenum定義を取得できません。");
    var display = field.GetCustomAttributes(typeof(DisplayAttribute), false)
        .OfType<DisplayAttribute>().SingleOrDefault();
    return display?.Name
        ?? throw new InvalidOperationException($"{typeof(TEnum).Name}.{value}の表示名を取得できません。");
}

static string GetDisplayDescription<TEnum>(TEnum value) where TEnum : struct, Enum
{
    var field = typeof(TEnum).GetField(value.ToString())
        ?? throw new InvalidOperationException($"{typeof(TEnum).Name}のenum定義を取得できません。");
    var display = field.GetCustomAttributes(typeof(DisplayAttribute), false)
        .OfType<DisplayAttribute>().SingleOrDefault();
    return display?.Description
        ?? throw new InvalidOperationException($"{typeof(TEnum).Name}.{value}の説明文を取得できません。");
}

static T GetPrivateConstant<T>(Type type, string fieldName)
{
    var field = type.GetField(fieldName, BindingFlags.Static | BindingFlags.NonPublic)
        ?? throw new InvalidOperationException($"{type.Name}.{fieldName}を取得できません。");
    return field.GetRawConstantValue() is T value
        ? value
        : throw new InvalidOperationException($"{type.Name}.{fieldName}の型が不正です。");
}

static bool HasPropertyCustomAttribute(
    string assemblyPath,
    string typeNamespace,
    string typeName,
    string propertyName,
    string attributeNamespace,
    string attributeName)
{
    using var stream = File.OpenRead(assemblyPath);
    using var peReader = new PEReader(stream);
    var metadata = peReader.GetMetadataReader();
    foreach (var typeHandle in metadata.TypeDefinitions)
    {
        var typeDefinition = metadata.GetTypeDefinition(typeHandle);
        if (metadata.GetString(typeDefinition.Namespace) != typeNamespace ||
            metadata.GetString(typeDefinition.Name) != typeName)
        {
            continue;
        }

        foreach (var propertyHandle in typeDefinition.GetProperties())
        {
            var propertyDefinition = metadata.GetPropertyDefinition(propertyHandle);
            if (metadata.GetString(propertyDefinition.Name) != propertyName)
            {
                continue;
            }

            foreach (var attributeHandle in propertyDefinition.GetCustomAttributes())
            {
                var attribute = metadata.GetCustomAttribute(attributeHandle);
                if (attribute.Constructor.Kind == HandleKind.MethodDefinition)
                {
                    var methodConstructor = metadata.GetMethodDefinition(
                        (MethodDefinitionHandle)attribute.Constructor);
                    var internalAttributeType = metadata.GetTypeDefinition(
                        methodConstructor.GetDeclaringType());
                    if (metadata.GetString(internalAttributeType.Namespace) == attributeNamespace &&
                        metadata.GetString(internalAttributeType.Name) == attributeName)
                    {
                        return true;
                    }

                    continue;
                }

                if (attribute.Constructor.Kind != HandleKind.MemberReference)
                {
                    continue;
                }

                var constructor = metadata.GetMemberReference(
                    (MemberReferenceHandle)attribute.Constructor);
                if (constructor.Parent.Kind != HandleKind.TypeReference)
                {
                    continue;
                }

                var attributeType = metadata.GetTypeReference(
                    (TypeReferenceHandle)constructor.Parent);
                if (metadata.GetString(attributeType.Namespace) == attributeNamespace &&
                    metadata.GetString(attributeType.Name) == attributeName)
                {
                    return true;
                }
            }

            return false;
        }

        return false;
    }

    return false;
}

static bool HasMethodDefinition(
    string assemblyPath,
    string typeNamespace,
    string typeName,
    string methodName)
{
    using var stream = File.OpenRead(assemblyPath);
    using var peReader = new PEReader(stream);
    var metadata = peReader.GetMetadataReader();
    foreach (var typeHandle in metadata.TypeDefinitions)
    {
        var typeDefinition = metadata.GetTypeDefinition(typeHandle);
        if (metadata.GetString(typeDefinition.Namespace) != typeNamespace ||
            metadata.GetString(typeDefinition.Name) != typeName)
        {
            continue;
        }

        foreach (var methodHandle in typeDefinition.GetMethods())
        {
            var methodDefinition = metadata.GetMethodDefinition(methodHandle);
            if (metadata.GetString(methodDefinition.Name) == methodName)
            {
                return true;
            }
        }

        return false;
    }

    return false;
}

static bool HasFieldDefinition(
    string assemblyPath,
    string typeNamespace,
    string typeName,
    string fieldName)
{
    using var stream = File.OpenRead(assemblyPath);
    using var peReader = new PEReader(stream);
    var metadata = peReader.GetMetadataReader();
    foreach (var typeHandle in metadata.TypeDefinitions)
    {
        var typeDefinition = metadata.GetTypeDefinition(typeHandle);
        if (metadata.GetString(typeDefinition.Namespace) != typeNamespace ||
            metadata.GetString(typeDefinition.Name) != typeName)
        {
            continue;
        }

        foreach (var fieldHandle in typeDefinition.GetFields())
        {
            var fieldDefinition = metadata.GetFieldDefinition(fieldHandle);
            if (metadata.GetString(fieldDefinition.Name) == fieldName)
            {
                return true;
            }
        }

        return false;
    }

    return false;
}

static bool HasVisibilityCondition(
    string source,
    string propertyName,
    string conditionName)
{
    var doubleProperty = source.IndexOf(
        $"public double {propertyName}",
        StringComparison.Ordinal);
    var boolProperty = source.IndexOf(
        $"public bool {propertyName}",
        StringComparison.Ordinal);
    var animationProperty = source.IndexOf(
        $"public Animation {propertyName}",
        StringComparison.Ordinal);
    var colorProperty = source.IndexOf(
        $"public Color {propertyName}",
        StringComparison.Ordinal);
    var propertyIndex = new[]
    {
        doubleProperty,
        boolProperty,
        animationProperty,
        colorProperty,
    }.Max();
    if (propertyIndex < 0)
    {
        return false;
    }

    var displayIndex = source.LastIndexOf(
        "[Display(",
        propertyIndex,
        StringComparison.Ordinal);
    var visibilityIndex = source.LastIndexOf(
        "[ShowPropertyEditorWhen(",
        propertyIndex,
        StringComparison.Ordinal);
    if (displayIndex < 0 || visibilityIndex < displayIndex)
    {
        return false;
    }

    var attributeBlock = source[visibilityIndex..propertyIndex];
    return attributeBlock.Contains(
        $"[ShowPropertyEditorWhen(nameof({conditionName}), true)]",
        StringComparison.Ordinal);
}

static void VerifyPathFingerprintAndUpdatePlan()
{
    var prefixSamples = new[]
    {
        Sample(0, 0.1f, 0.2f, 1),
        Sample(1, 0.2f, 0.3f, 1),
    };
    var extendedSamples = new[]
    {
        prefixSamples[0],
        prefixSamples[1],
        Sample(2, 0.3f, 0.4f, 1),
    };
    var changedSamples = new[]
    {
        prefixSamples[0],
        Sample(1, 0.25f, 0.3f, 1),
    };
    var brushShapeChangedSamples = new[]
    {
        Sample(
            0,
            0.1f,
            0.2f,
            1,
            aspectRatio: 2,
            rotationRadians: 0.25f),
        prefixSamples[1],
    };
    var accumulationGroupChangedSamples = new[]
    {
        prefixSamples[0] with { AccumulationGroup = 1 },
        prefixSamples[1],
    };
    var prefix = Snapshot(prefixSamples);
    var same = Snapshot(prefixSamples);
    var extended = Snapshot(extendedSamples);
    var changed = Snapshot(changedSamples);
    var brushShapeChanged = Snapshot(brushShapeChangedSamples);
    var accumulationGroupChanged = Snapshot(accumulationGroupChangedSamples);
    var geometry = WipeMaskGeometry.Create(800, 600);

    Equal(17, prefix.Fingerprint.SchemaVersion, "Path Cacheのスキーマバージョンが不正です。");
    Equal(prefix.Fingerprint, same.Fingerprint, "同じPathのFingerprintが一致しません。");
    True(prefix.Fingerprint != changed.Fingerprint, "Path変更でFingerprintが変化していません。");
    True(
        prefix.Fingerprint != brushShapeChanged.Fingerprint,
        "ブラシ縦横比・回転の変更をFingerprintへ含める必要があります。");
    True(
        prefix.Fingerprint != accumulationGroupChanged.Fingerprint,
        "片道累積グループの変更をFingerprintへ含める必要があります。");

    var styledPrefix = Snapshot(
        prefixSamples,
        new WipePathStyle(0.5f, 0.01f, 123));
    var styledExtended = Snapshot(
        extendedSamples,
        new WipePathStyle(0.5f, 0.01f, 123));
    var smoothingChanged = Snapshot(
        prefixSamples,
        new WipePathStyle(0.6f, 0.01f, 123));
    var jitterChanged = Snapshot(
        prefixSamples,
        new WipePathStyle(0.5f, 0.02f, 123));
    var seedChanged = Snapshot(
        prefixSamples,
        new WipePathStyle(0.5f, 0.01f, 124));
    var rotationFollowChanged = Snapshot(
        prefixSamples,
        new WipePathStyle(0.5f, 0.01f, 123, 0.5f));
    var softnessChanged = Snapshot(
        prefixSamples,
        new WipePathStyle(0.5f, 0.01f, 123, 0, 0.5f));
    var qualityChanged = Snapshot(
        prefixSamples,
        new WipePathStyle(0.5f, 0.01f, 123, 0, 0, GlassWipeQuality.High));
    var accumulationModeChanged = Snapshot(
        prefixSamples,
        new WipePathStyle(
            0.5f,
            0.01f,
            123,
            0,
            0,
            GlassWipeQuality.Standard,
            WipeMaskAccumulationMode.PerPassMaximum));
    var brushStyleShapeChanged = Snapshot(
        prefixSamples,
        new WipePathStyle(
            0.5f,
            0.01f,
            123,
            BrushShape: GlassWipeBrushShape.Rectangle));
    var initialTangentChanged = Snapshot(
        prefixSamples,
        new WipePathStyle(
            0.5f,
            0.01f,
            123,
            InitialTangentX: 0.25f,
            InitialTangentY: -0.5f));
    var brushMirrorChanged = Snapshot(
        prefixSamples,
        new WipePathStyle(
            0.5f,
            0.01f,
            123,
            BrushShape: GlassWipeBrushShape.Hand,
            BrushMirror: true));
    True(
        styledPrefix.Fingerprint != smoothingChanged.Fingerprint &&
        styledPrefix.Fingerprint != jitterChanged.Fingerprint &&
        styledPrefix.Fingerprint != seedChanged.Fingerprint &&
        styledPrefix.Fingerprint != rotationFollowChanged.Fingerprint &&
        styledPrefix.Fingerprint != softnessChanged.Fingerprint &&
        styledPrefix.Fingerprint != qualityChanged.Fingerprint &&
        styledPrefix.Fingerprint != accumulationModeChanged.Fingerprint &&
        styledPrefix.Fingerprint != brushStyleShapeChanged.Fingerprint &&
        styledPrefix.Fingerprint != initialTangentChanged.Fingerprint &&
        styledPrefix.Fingerprint != brushMirrorChanged.Fingerprint,
        "軌跡整形・回転追従・柔らかさ・品質・累積方式・ブラシ形状・開始方向・左右反転をFingerprintへ含める必要があります。");
    Equal(
        default(WipePathStyle),
        Snapshot(
            prefixSamples,
            new WipePathStyle(float.NaN, float.PositiveInfinity, -1)).Style,
        "非有限または範囲外の軌跡整形設定を安全な既定値へ補正する必要があります。");
    var sanitizedInitialTangent = new WipePathStyle(
        0,
        0,
        0,
        InitialTangentX: float.NaN,
        InitialTangentY: float.PositiveInfinity).Sanitize();
    True(
        sanitizedInitialTangent.InitialTangentX == 0 &&
        sanitizedInitialTangent.InitialTangentY == 0,
        "非有限の開始方向を0へ補正する必要があります。");
    Equal(
        WipeMaskAccumulationMode.SourceOver,
        new WipePathStyle(
            0,
            0,
            0,
            0,
            0,
            GlassWipeQuality.Standard,
            (WipeMaskAccumulationMode)99).Sanitize().AccumulationMode,
        "不正な累積方式はSourceOverへ補正する必要があります。");

    var shapeChangedPlan = WipeMaskUpdatePlan.Create(
        styledPrefix,
        geometry,
        brushStyleShapeChanged,
        geometry);
    Equal(
        WipeMaskUpdateKind.Rebuild,
        shapeChangedPlan.Kind,
        "ブラシ形状変更時は累積マスクを完全再構築する必要があります。");

    var styledAppend = WipeMaskUpdatePlan.Create(
        styledPrefix,
        geometry,
        styledExtended,
        geometry);
    Equal(
        WipeMaskUpdateKind.Append,
        styledAppend.Kind,
        "同じ軌跡整形設定での前進は差分追加する必要があります。");
    var styleEdited = WipeMaskUpdatePlan.Create(
        styledPrefix,
        geometry,
        smoothingChanged,
        geometry);
    Equal(
        WipeMaskUpdateKind.Rebuild,
        styleEdited.Kind,
        "軌跡整形設定の変更後に古いMaskを再利用してはいけません。");

    var reuse = WipeMaskUpdatePlan.Create(prefix, geometry, same, geometry);
    Equal(WipeMaskUpdateKind.Reuse, reuse.Kind, "同じフレームとPathは再利用する必要があります。");

    var append = WipeMaskUpdatePlan.Create(prefix, geometry, extended, geometry);
    Equal(WipeMaskUpdateKind.Append, append.Kind, "前進時は未描画区間を追加する必要があります。");
    Equal(prefixSamples.Length, append.FirstSampleIndex, "差分追加の開始位置が不正です。");

    var backward = WipeMaskUpdatePlan.Create(extended, geometry, prefix, geometry);
    Equal(WipeMaskUpdateKind.Rebuild, backward.Kind, "後方シークは完全再構築する必要があります。");

    var edited = WipeMaskUpdatePlan.Create(prefix, geometry, changed, geometry);
    Equal(WipeMaskUpdateKind.Rebuild, edited.Kind, "履歴編集後に古いMaskを再利用してはいけません。");

    var changedGeometry = WipeMaskGeometry.Create(600, 800);
    var resized = WipeMaskUpdatePlan.Create(prefix, geometry, same, changedGeometry);
    Equal(WipeMaskUpdateKind.Rebuild, resized.Kind, "Regionの公称Aspect Ratio変更では再構築が必要です。");

    var customPrefix = Snapshot(
        prefixSamples,
        inputMode: WipePathInputMode.StrokeCollection,
        pathDataSignature: "v1:custom-a");
    var customSame = Snapshot(
        prefixSamples,
        inputMode: WipePathInputMode.StrokeCollection,
        pathDataSignature: "v1:custom-a");
    var customExtended = Snapshot(
        extendedSamples,
        inputMode: WipePathInputMode.StrokeCollection,
        pathDataSignature: "v1:custom-a");
    var customEdited = Snapshot(
        prefixSamples,
        inputMode: WipePathInputMode.StrokeCollection,
        pathDataSignature: "v1:custom-b");
    Equal(
        WipeMaskUpdateKind.Reuse,
        WipeMaskUpdatePlan.Create(customPrefix, geometry, customSame, geometry).Kind,
        "完全一致するカスタム軌跡は再利用する必要があります。");
    Equal(
        WipeMaskUpdateKind.Rebuild,
        WipeMaskUpdatePlan.Create(customPrefix, geometry, customExtended, geometry).Kind,
        "カスタム軌跡の前進時に危険な差分追加をしてはいけません。");
    Equal(
        WipeMaskUpdateKind.Rebuild,
        WipeMaskUpdatePlan.Create(customPrefix, geometry, customEdited, geometry).Kind,
        "カスタム軌跡編集後は完全再構築する必要があります。");
    Equal(
        WipeMaskUpdateKind.Rebuild,
        WipeMaskUpdatePlan.Create(prefix, geometry, customPrefix, geometry).Kind,
        "入力方式を切り替えた後は完全再構築する必要があります。");
    True(
        customPrefix.Fingerprint != customEdited.Fingerprint &&
        prefix.Fingerprint != customPrefix.Fingerprint,
        "入力方式と保存データ署名をFingerprintへ含める必要があります。");
}

static void VerifyLongPathStreamingBounds()
{
    const int finalFrame = 2_160_000;
    var requestedRangeLengths = new List<int>();
    var stream = new WipePathStream(
        finalFrame,
        finalFrame,
        60,
        1,
        default,
        WipePathStreamKind.LegacyAnimation,
        new WipePathDefinitionFingerprint(1),
        (first, last, sink) =>
        {
            requestedRangeLengths.Add(checked(last - first + 1));
            for (var frame = first; ; frame++)
            {
                sink(0, new WipePathSample(frame, 0.5f, 0.5f, 1, 0.1f, 1, 1, 0));
                if (frame == last)
                {
                    break;
                }
            }
        });

    long sampleCount = 0;
    stream.EnumerateSamples(0, finalFrame, (_, _) => sampleCount++);
    Equal((long)finalFrame + 1, sampleCount, "10時間相当の全Sampleを切り捨てず列挙する必要があります。");
    True(
        requestedRangeLengths.Count > 1 &&
        requestedRangeLengths.All(length => length <= WipePathStream.SampleBatchCapacity),
        "Path Streamが2,048件を超える範囲を一度に要求してはいけません。");
    Equal(
        (int)(((long)finalFrame + 1 + WipePathStream.SampleBatchCapacity - 1) /
            WipePathStream.SampleBatchCapacity),
        requestedRangeLengths.Count,
        "長尺列挙のバッチ数が不正です。");

    requestedRangeLengths.Clear();
    var maximumFrameStream = new WipePathStream(
        int.MaxValue,
        int.MaxValue,
        60,
        1,
        default,
        WipePathStreamKind.LegacyAnimation,
        new WipePathDefinitionFingerprint(2),
        (first, last, sink) =>
        {
            requestedRangeLengths.Add(checked(last - first + 1));
            for (var frame = first; ; frame++)
            {
                sink(0, new WipePathSample(frame, 0.5f, 0.5f, 1, 0.1f, 1, 1, 0));
                if (frame == last)
                {
                    break;
                }
            }
        });
    sampleCount = 0;
    maximumFrameStream.EnumerateSamples(
        int.MaxValue - WipePathStream.SampleBatchCapacity,
        int.MaxValue,
        (_, _) => sampleCount++);
    Equal(
        (long)WipePathStream.SampleBatchCapacity + 1,
        sampleCount,
        "int.MaxValue近傍でも終端Sampleまで列挙する必要があります。");
    Equal(2, requestedRangeLengths.Count, "int.MaxValue近傍の列挙は2バッチになる必要があります。");
    True(
        requestedRangeLengths.All(length => length <= WipePathStream.SampleBatchCapacity),
        "int.MaxValue近傍でもバッチ上限を超えてはいけません。");
}

static void VerifyStreamUpdatePlanAndDefinitionFingerprint()
{
    var geometry = WipeMaskGeometry.Create(1920, 1080);
    var changedGeometry = WipeMaskGeometry.Create(1080, 1920);
    var sameDefinition = new WipePathDefinitionFingerprint(10);
    var changedDefinition = new WipePathDefinitionFingerprint(11);

    WipePathStream CreatePlanStream(
        int frame,
        WipePathDefinitionFingerprint fingerprint) =>
        new(
            frame,
            1000,
            60,
            1,
            default,
            WipePathStreamKind.LegacyAnimation,
            fingerprint,
            (_, _, _) => { });

    var cached = CreatePlanStream(100, sameDefinition);
    var reused = WipeMaskUpdatePlan.Create(
        cached,
        geometry,
        CreatePlanStream(100, sameDefinition),
        geometry);
    Equal(WipeMaskUpdateKind.Reuse, reused.Kind, "同じDefinitionとフレームは再利用する必要があります。");

    var appended = WipeMaskUpdatePlan.Create(
        cached,
        geometry,
        CreatePlanStream(101, sameDefinition),
        geometry);
    Equal(WipeMaskUpdateKind.Append, appended.Kind, "同じDefinitionでの前進は差分追加する必要があります。");
    Equal(101, appended.FirstSampleIndex, "Stream差分追加の開始フレームが不正です。");

    Equal(
        WipeMaskUpdateKind.Rebuild,
        WipeMaskUpdatePlan.Create(
            cached,
            geometry,
            CreatePlanStream(99, sameDefinition),
            geometry).Kind,
        "Streamの後方シークは再構築する必要があります。");
    Equal(
        WipeMaskUpdateKind.Rebuild,
        WipeMaskUpdatePlan.Create(
            cached,
            geometry,
            CreatePlanStream(100, changedDefinition),
            geometry).Kind,
        "同一フレームでもDefinition変更後は再構築する必要があります。");
    Equal(
        WipeMaskUpdateKind.Rebuild,
        WipeMaskUpdatePlan.Create(
            cached,
            geometry,
            CreatePlanStream(101, changedDefinition),
            geometry).Kind,
        "Definition変更後は再構築する必要があります。");
    Equal(
        WipeMaskUpdateKind.Rebuild,
        WipeMaskUpdatePlan.Create(
            cached,
            geometry,
            CreatePlanStream(101, sameDefinition),
            changedGeometry).Kind,
        "Streamのgeometry変更後は再構築する必要があります。");

    var beforeStart = CreatePlanStream(-1, sameDefinition);
    var started = WipeMaskUpdatePlan.Create(
        beforeStart,
        geometry,
        CreatePlanStream(0, sameDefinition),
        geometry);
    Equal(WipeMaskUpdateKind.Append, started.Kind, "描画開始時はフレーム0だけを追加する必要があります。");
    Equal(0, started.FirstSampleIndex, "描画開始時の追加開始フレームが不正です。");

    var bezierPoints = new[]
    {
        new WipeAnimationBezierFingerprintPoint(0, 0, 0.2f, 0.3f, 0.8f, 0.7f),
    };
    var initialFingerprint = WipeAnimationDefinitionFingerprint.Create(
        0,
        "1",
        "60",
        "False",
        [0.ToString()],
        [0.ToString()],
        false,
        bezierPoints);
    var valueChangedFingerprint = WipeAnimationDefinitionFingerprint.Create(
        0,
        "1",
        "60",
        "False",
        [0.ToString()],
        [25.ToString()],
        false,
        bezierPoints);
    True(
        initialFingerprint != valueChangedFingerprint,
        "Animation値変更をDefinition Fingerprintへ含める必要があります。");

    var typeChangedFingerprint = WipeAnimationDefinitionFingerprint.Create(
        1,
        "1",
        "60",
        "False",
        [0.ToString()],
        [25.ToString()],
        false,
        bezierPoints);
    True(
        valueChangedFingerprint != typeChangedFingerprint,
        "Animation種別変更をDefinition Fingerprintへ含める必要があります。");

    var bezierChangedFingerprint = WipeAnimationDefinitionFingerprint.Create(
        1,
        "1",
        "60",
        "False",
        [0.ToString()],
        [25.ToString()],
        true,
        bezierPoints);
    True(
        typeChangedFingerprint != bezierChangedFingerprint,
        "Bezier設定変更をDefinition Fingerprintへ含める必要があります。");

    var keyFrameChangedFingerprint = WipeAnimationDefinitionFingerprint.Create(
        1,
        "1",
        "60",
        "False",
        [0.ToString(), 17.ToString()],
        [25.ToString()],
        true,
        bezierPoints);
    True(
        bezierChangedFingerprint != keyFrameChangedFingerprint,
        "過去キーフレーム変更をDefinition Fingerprintへ含める必要があります。");
}

static void VerifyStreamingStampCompatibility()
{
    var samples = Enumerable.Range(0, 14)
        .Select(index => new WipePathSample(
            index,
            index % 2 == 0 ? 0.05f : 0.95f,
            index % 3 == 0 ? 0.08f : 0.92f,
            index == 5 ? 0 : 1,
            0.01f,
            0.65f,
            1.4f,
            0.1f,
            index < 8 ? 0 : 1))
        .ToArray();
    var geometry = WipeMaskGeometry.Create(1024, 1024);
    var styles = new[]
    {
        default(WipePathStyle),
        new WipePathStyle(
            0.65f,
            0.02f,
            321,
            0.8f,
            0.25f,
            GlassWipeQuality.High,
            WipeMaskAccumulationMode.SourceOver,
            GlassWipeBrushShape.Hand,
            InitialTangentX: 0.4f,
            InitialTangentY: -0.2f),
    };

    foreach (var style in styles)
    {
        var expected = WipeBrushStampGenerator.Generate(
            samples,
            0,
            geometry,
            style);
        var collector = new BoundedStampCollector(
            WipeBrushStampGenerator.StampBatchCapacity);
        var state = new WipeBrushStampGenerator.StreamState(0);
        foreach (var sample in samples)
        {
            state.Append(sample, geometry, style, collector);
        }

        collector.Flush();
        Equal(expected.Count, collector.Items.Count, "Stream Stamp数が従来結果と一致しません。");
        for (var index = 0; index < expected.Count; index++)
        {
            Equal(
                expected[index],
                collector.Items[index],
                $"Stream Stampの全フィールドが従来結果と一致しません。Index={index}");
        }
        True(
            collector.MaximumBufferedCount <= WipeBrushStampGenerator.StampBatchCapacity,
            "Stampを4,096件より多く一時保持してはいけません。");
    }

    True(
        WipeBrushStampGenerator.Generate(samples, 0, geometry, styles[1]).Count >
            WipeBrushStampGenerator.StampBatchCapacity,
        "固定容量境界をまたぐ十分なStampを生成する試験である必要があります。");
}

static void VerifyMergedStrokeStreaming()
{
    var document = new WipeStrokeDocument
    {
        Strokes =
        [
            new WipeStroke
            {
                Points =
                [
                    PercentStrokePoint(0, 10, 15, 100),
                    PercentStrokePoint(50, 40, 45, 100),
                    PercentStrokePoint(50, 45, 50, 0),
                    PercentStrokePoint(75, 65, 30, 100),
                    PercentStrokePoint(100, 90, 20, 100),
                ],
            },
            new WipeStroke
            {
                Points =
                [
                    PercentStrokePoint(0, 90, 85, 100),
                    PercentStrokePoint(50, 60, 55, 100),
                    PercentStrokePoint(100, 10, 80, 100),
                ],
            },
        ],
    };
    const int itemLength = 20;
    var merged = WipeStrokeSampler.EnumerateMerged(
            document,
            0,
            itemLength,
            itemLength)
        .ToArray();
    True(merged.Length > 0, "複数StrokeのStream Sampleが必要です。");
    for (var index = 1; index < merged.Length; index++)
    {
        True(
            merged[index - 1].Sample.Frame <= merged[index].Sample.Frame,
            "複数Strokeをフレーム順に列挙する必要があります。");
        if (merged[index - 1].Sample.Frame == merged[index].Sample.Frame)
        {
            True(
                merged[index - 1].LaneId <= merged[index].LaneId,
                "同一フレームはStroke保存順で列挙する必要があります。");
        }
    }

    var geometry = WipeMaskGeometry.Create(1920, 1080);
    var style = new WipePathStyle(
        0.55f,
        0.015f,
        77,
        0.6f,
        0.15f,
        GlassWipeQuality.Standard,
        WipeMaskAccumulationMode.SourceOver,
        GlassWipeBrushShape.ShoePrint);
    var oldSamples = WipeStrokeSampler.Expand(document, itemLength, itemLength)
        .Select(sample => new WipePathSample(
            sample.Frame,
            sample.X,
            sample.Y,
            sample.Contact,
            0.06f,
            0.7f,
            1.2f,
            0.2f))
        .ToArray();
    var expected = WipeBrushStampGenerator.Generate(
        oldSamples,
        0,
        geometry,
        style);

    var strokeIdBases = WipeStrokeSampler.GetStrokeIdBases(document);
    var states = strokeIdBases
        .Select(value => new WipeBrushStampGenerator.StreamState(value))
        .ToArray();
    var collector = new BoundedStampCollector(
        WipeBrushStampGenerator.StampBatchCapacity);
    foreach (var (laneId, frameSample) in merged)
    {
        states[laneId].Append(
            new WipePathSample(
                frameSample.Frame,
                frameSample.X,
                frameSample.Y,
                frameSample.Contact,
                0.06f,
                0.7f,
                1.2f,
                0.2f),
            geometry,
            style,
            collector);
    }

    collector.Flush();
    Equal(expected.Count, collector.Items.Count, "複数StrokeのStamp数を維持する必要があります。");
    var expectedCounts = expected
        .GroupBy(stamp => stamp)
        .ToDictionary(group => group.Key, group => group.Count());
    var actualCounts = collector.Items
        .GroupBy(stamp => stamp)
        .ToDictionary(group => group.Key, group => group.Count());
    Equal(expectedCounts.Count, actualCounts.Count, "複数StrokeのStamp集合が一致しません。");
    foreach (var (stamp, count) in expectedCounts)
    {
        if (!actualCounts.TryGetValue(stamp, out var actualCount) || actualCount != count)
        {
            throw new InvalidOperationException(
                "複数Strokeの分離、弧長、固定Seed揺れを維持する必要があります。" +
                $" ExpectedCount={count}, ActualCount={actualCount}, Stamp={stamp}");
        }
    }
}

static void VerifyRandomSeekDeterminism()
{
    const int frameCount = 121;
    var allSamples = Enumerable.Range(0, frameCount)
        .Select(frame =>
        {
            var amount = frame / (float)(frameCount - 1);
            var contact = frame is >= 30 and <= 39 or >= 80 and <= 84 ? 0 : 1;
            return Sample(
                frame,
                0.1f + amount * 0.8f,
                0.5f + MathF.Sin(amount * MathF.PI * 4) * 0.25f,
                contact,
                0.08f + amount * 0.04f,
                0.6f + amount * 0.4f);
        })
        .ToArray();
    var geometry = WipeMaskGeometry.Create(1920, 1080);
    var style = new WipePathStyle(
        0.7f,
        0.015f,
        20260823,
        0.8f,
        0.35f,
        GlassWipeQuality.High);
    var probes = new[]
    {
        new Vector2(0.15f, 0.5f),
        new Vector2(0.35f, 0.7f),
        new Vector2(0.5f, 0.5f),
        new Vector2(0.7f, 0.3f),
        new Vector2(0.9f, 0.5f),
    };
    var baselines = new float[frameCount][];
    for (var frame = 0; frame < frameCount; frame++)
    {
        var path = Snapshot(allSamples[..(frame + 1)], style);
        var stamps = WipeBrushStampGenerator.Generate(
            path.Samples,
            0,
            geometry,
            path.Style);
        baselines[frame] = ApplyStamps(new float[probes.Length], probes, stamps);
    }

    WipePathSnapshot? cachedPath = null;
    WipeMaskGeometry? cachedGeometry = null;
    var cachedValues = new float[probes.Length];
    var random = new Random(20260822);
    for (var iteration = 0; iteration < 1000; iteration++)
    {
        var frame = random.Next(frameCount);
        var currentPath = Snapshot(allSamples[..(frame + 1)], style);
        var plan = WipeMaskUpdatePlan.Create(
            cachedPath,
            cachedGeometry,
            currentPath,
            geometry);

        if (plan.Kind == WipeMaskUpdateKind.Rebuild)
        {
            Array.Clear(cachedValues);
        }

        if (plan.Kind != WipeMaskUpdateKind.Reuse)
        {
            var stamps = WipeBrushStampGenerator.Generate(
                currentPath.Samples,
                plan.FirstSampleIndex,
                geometry,
                currentPath.Style);
            ApplyStamps(cachedValues, probes, stamps);
        }

        for (var probeIndex = 0; probeIndex < probes.Length; probeIndex++)
        {
            NearlyEqual(
                baselines[frame][probeIndex],
                cachedValues[probeIndex],
                $"ランダムシーク{iteration + 1}回目、frame={frame}で基準結果と一致しません。");
        }

        cachedPath = currentPath;
        cachedGeometry = geometry;
    }
}

static void VerifyCustomRandomSeekDeterminism()
{
    const int frameCount = 121;
    var document = new WipeStrokeDocument
    {
        Strokes =
        [
            new WipeStroke
            {
                Points =
                [
                    PercentStrokePoint(5, 10, 30, 100),
                    PercentStrokePoint(40, 50, 20, 80),
                    PercentStrokePoint(80, 90, 30, 100),
                ],
            },
            new WipeStroke
            {
                Points =
                [
                    PercentStrokePoint(10, 90, 70, 100),
                    PercentStrokePoint(35, 50, 80, 60),
                    PercentStrokePoint(60, 10, 70, 100),
                ],
            },
        ],
    };
    var signature = WipeStrokeDocumentCodec.Encode(document);
    var geometry = WipeMaskGeometry.Create(1920, 1080);
    var style = new WipePathStyle(
        0.7f,
        0.015f,
        20260823,
        0.8f,
        0.35f,
        GlassWipeQuality.High);
    var probes = new[]
    {
        new Vector2(0.1f, 0.3f),
        new Vector2(0.3f, 0.5f),
        new Vector2(0.5f, 0.2f),
        new Vector2(0.5f, 0.8f),
        new Vector2(0.7f, 0.5f),
        new Vector2(0.9f, 0.7f),
    };
    var baselines = new float[frameCount][];
    for (var frame = 0; frame < frameCount; frame++)
    {
        var path = CustomSnapshot(document, frame, style, signature);
        var stamps = WipeBrushStampGenerator.Generate(
            path.Samples,
            0,
            geometry,
            path.Style);
        baselines[frame] = ApplyStamps(new float[probes.Length], probes, stamps);
    }

    WipePathSnapshot? cachedPath = null;
    WipeMaskGeometry? cachedGeometry = null;
    var cachedValues = new float[probes.Length];
    var random = new Random(20260823);
    for (var iteration = 0; iteration < 1000; iteration++)
    {
        var frame = random.Next(frameCount);
        var currentPath = CustomSnapshot(document, frame, style, signature);
        var plan = WipeMaskUpdatePlan.Create(
            cachedPath,
            cachedGeometry,
            currentPath,
            geometry);
        True(
            plan.Kind != WipeMaskUpdateKind.Append,
            "カスタム軌跡へAppend更新を適用してはいけません。");

        if (plan.Kind == WipeMaskUpdateKind.Rebuild)
        {
            Array.Clear(cachedValues);
        }

        if (plan.Kind != WipeMaskUpdateKind.Reuse)
        {
            var stamps = WipeBrushStampGenerator.Generate(
                currentPath.Samples,
                plan.FirstSampleIndex,
                geometry,
                currentPath.Style);
            ApplyStamps(cachedValues, probes, stamps);
        }

        for (var probeIndex = 0; probeIndex < probes.Length; probeIndex++)
        {
            NearlyEqual(
                baselines[frame][probeIndex],
                cachedValues[probeIndex],
                $"カスタム軌跡シーク{iteration + 1}回目、frame={frame}で基準結果と一致しません。");
        }

        cachedPath = currentPath;
        cachedGeometry = geometry;
    }
}

static void VerifyEmbeddedShader()
{
    var compilerSource = RemoveWhitespace(File.ReadAllText(
        Path.Combine(
            FindRepositoryRoot(),
            "tools",
            "YMM4GlassWipe.ShaderCompiler",
            "Program.cs")));
    var debugStart = compilerSource.IndexOf("#ifDEBUG", StringComparison.Ordinal);
    var releaseStart = compilerSource.IndexOf(
        "#else",
        Math.Max(debugStart, 0),
        StringComparison.Ordinal);
    var branchEnd = compilerSource.IndexOf(
        "#endif",
        Math.Max(releaseStart, 0),
        StringComparison.Ordinal);
    True(
        debugStart >= 0 && releaseStart > debugStart && branchEnd > releaseStart,
        "シェーダー最適化フラグのDebug/Release分岐が見つかりません。");

    var debugBranch = compilerSource[debugStart..releaseStart];
    var releaseBranch = compilerSource[releaseStart..branchEnd];
    True(
        debugBranch.Contains("ShaderFlags.OptimizationLevel0", StringComparison.Ordinal) &&
        !debugBranch.Contains("ShaderFlags.OptimizationLevel3", StringComparison.Ordinal) &&
        !debugBranch.Contains("ShaderFlags.SkipOptimization", StringComparison.Ordinal),
        "Debug版はO3または最適化無効を併用せず、O0を使用する必要があります。");
    True(
        releaseBranch.Contains("ShaderFlags.OptimizationLevel3", StringComparison.Ordinal) &&
        !releaseBranch.Contains("ShaderFlags.OptimizationLevel0", StringComparison.Ordinal) &&
        !releaseBranch.Contains("ShaderFlags.SkipOptimization", StringComparison.Ordinal),
        "Release版はO0または最適化無効を併用せず、O3を維持する必要があります。");

    var compileBlock = compilerSource[branchEnd..];
    True(
        compileBlock.Contains(
            "varcompileFlags=ShaderFlags.EnableStrictness|ShaderFlags.WarningsAreErrors|optimizationFlags;",
            StringComparison.Ordinal) &&
        compileBlock.Contains(
            "\"ps_4_0\",compileFlags,EffectFlags.None",
            StringComparison.Ordinal),
        "選択した最適化フラグをps_4_0のコンパイルへ渡す必要があります。");
    True(
        compileBlock.Contains("[{optimizationName}]", StringComparison.Ordinal),
        "ビルドログへ使用したシェーダー最適化モードを表示する必要があります。");

    const string resourceName = "YMM4GlassWipe.Shaders.GlassComposite.cso";
    using var stream = typeof(GlassCompositeConstants).Assembly
        .GetManifestResourceStream(resourceName);

    if (stream is null || stream.Length == 0)
    {
        throw new InvalidOperationException(
            $"埋め込みリソース {resourceName} が見つからないか空です。");
    }
}

static Vector4 CompositeReference(
    Vector4 original,
    Vector4 fogged,
    float regionMask,
    float fogAmount,
    float wipeMask)
{
    var fogMask = Math.Clamp(
        regionMask * fogAmount * (1 - wipeMask),
        0,
        1);
    var result = Vector4.Lerp(original, fogged, fogMask);
    result.W = original.W;
    return result;
}

static WipePathSample Sample(
    int frame,
    float x,
    float y,
    float contact,
    float size = 0.1f,
    float strength = 1,
    float aspectRatio = 1,
    float rotationRadians = 0,
    int accumulationGroup = 0) =>
    new(
        frame,
        x,
        y,
        contact,
        size,
        strength,
        aspectRatio,
        rotationRadians,
        accumulationGroup);

static WipeStrokePoint PercentStrokePoint(
    double timelinePercent,
    double xPercent,
    double yPercent,
    double contact) =>
    new(
        timelinePercent,
        xPercent / 100 * WipeStrokeDocument.CanvasPixelWidth,
        yPercent / 100 * WipeStrokeDocument.CanvasPixelHeight,
        contact);

static WipePathSnapshot Snapshot(
    IReadOnlyList<WipePathSample> samples,
    WipePathStyle style = default,
    WipePathInputMode inputMode = WipePathInputMode.LegacyAnimation,
    string? pathDataSignature = null) =>
    new(
        samples.Count == 0 ? 0 : samples[^1].Frame,
        120,
        60,
        samples,
        style,
        inputMode,
        pathDataSignature);

static WipePathSnapshot CustomSnapshot(
    WipeStrokeDocument document,
    int frame,
    WipePathStyle style,
    string pathDataSignature)
{
    var samples = WipeStrokeSampler.Expand(document, frame, 120)
        .Select(sample => Sample(
            sample.Frame,
            sample.X,
            sample.Y,
            sample.Contact,
            0.12f,
            0.85f,
            1.4f,
            0.3f))
        .ToArray();
    return new WipePathSnapshot(
        frame,
        120,
        60,
        samples,
        style,
        WipePathInputMode.StrokeCollection,
        pathDataSignature);
}

static float[] ApplyStamps(
    float[] values,
    IReadOnlyList<Vector2> probes,
    IReadOnlyList<WipeBrushStamp> stamps)
{
    foreach (var stamp in stamps)
    {
        for (var probeIndex = 0; probeIndex < probes.Count; probeIndex++)
        {
            var probe = probes[probeIndex] * WipeMaskRenderer.MaskSize;
            var normalizedX = (probe.X - stamp.Center.X) / stamp.RadiusX;
            var normalizedY = (probe.Y - stamp.Center.Y) / stamp.RadiusY;
            if (normalizedX * normalizedX + normalizedY * normalizedY <= 1)
            {
                values[probeIndex] = stamp.Alpha +
                    values[probeIndex] * (1 - stamp.Alpha);
            }
        }
    }

    return values;
}

static float[] ApplyStampsPerPassCoverageUnion(
    float[] values,
    IReadOnlyList<Vector2> probes,
    IReadOnlyList<WipeBrushStamp> stamps)
{
    foreach (var group in stamps.GroupBy(stamp => stamp.AccumulationGroup))
    {
        var groupedStamps = group.ToArray();
        var passOpacity = WipeMaskRenderer.ResolvePassOpacity(
            groupedStamps,
            0,
            groupedStamps.Length);
        if (passOpacity <= 0)
        {
            continue;
        }

        var passValues = new float[probes.Count];
        foreach (var stamp in groupedStamps)
        {
            var normalizedOpacity = Math.Clamp(
                stamp.Alpha / passOpacity,
                0,
                1);
            for (var probeIndex = 0; probeIndex < probes.Count; probeIndex++)
            {
                var probe = probes[probeIndex] * WipeMaskRenderer.MaskSize;
                var normalizedX = (probe.X - stamp.Center.X) / stamp.RadiusX;
                var normalizedY = (probe.Y - stamp.Center.Y) / stamp.RadiusY;
                if (normalizedX * normalizedX + normalizedY * normalizedY <= 1)
                {
                    passValues[probeIndex] =
                        WipeMaskRenderer.UsesMaximumPassCoverage(stamp.Shape)
                            ? MathF.Max(
                                passValues[probeIndex],
                                normalizedOpacity)
                            : normalizedOpacity +
                                passValues[probeIndex] *
                                (1 - normalizedOpacity);
                }
            }
        }

        for (var probeIndex = 0; probeIndex < probes.Count; probeIndex++)
        {
            var passValue = passValues[probeIndex] * passOpacity;
            values[probeIndex] = passValue +
                values[probeIndex] * (1 - passValue);
        }
    }

    return values;
}

static float EffectiveWipeMaskReference(
    float sampledWipeMask,
    float wipeResidue,
    float wipeVariation,
    Vector2 regionUv,
    float noiseSeed)
{
    var effectiveWipeMask = Math.Clamp(sampledWipeMask, 0, 1);
    if (wipeResidue <= 0 && wipeVariation <= 0)
    {
        return effectiveWipeMask;
    }

    var variationScale = 1f;
    if (wipeVariation > 0)
    {
        var wipeNoiseValue = ValueNoiseReference(regionUv * 48, noiseSeed);
        variationScale = LerpReference(
            1,
            0.65f + 0.35f * wipeNoiseValue,
            Math.Clamp(wipeVariation, 0, 1));
    }

    return Math.Clamp(
        effectiveWipeMask *
        (1 - Math.Clamp(wipeResidue, 0, 1)) *
        variationScale,
        0,
        1);
}

static float FogNoiseOffsetReference(
    Vector2 regionUv,
    float noiseSeed,
    float fogNoise)
{
    if (fogNoise <= 0)
    {
        return 0;
    }

    var noiseValue =
        ValueNoiseReference(regionUv * 24, noiseSeed + 17) * 2 - 1;
    return noiseValue * 0.12f * Math.Clamp(fogNoise, 0, 1);
}

static float ValueNoiseReference(Vector2 position, float seed)
{
    var lattice = new Vector2(
        MathF.Floor(position.X),
        MathF.Floor(position.Y));
    var blend = position - lattice;
    blend *= blend * (new Vector2(3) - 2 * blend);
    var lower = LerpReference(
        HashNoiseReference(lattice, seed),
        HashNoiseReference(lattice + new Vector2(1, 0), seed),
        blend.X);
    var upper = LerpReference(
        HashNoiseReference(lattice + new Vector2(0, 1), seed),
        HashNoiseReference(lattice + new Vector2(1, 1), seed),
        blend.X);
    return LerpReference(lower, upper, blend.Y);
}

static float HashNoiseReference(Vector2 lattice, float seed)
{
    var hashInput = Vector2.Dot(lattice, new Vector2(127.1f, 311.7f)) +
        seed * 74.7f;
    var value = MathF.Sin(hashInput) * 43758.5453f;
    return value - MathF.Floor(value);
}

static DetailedWipePresetState CreateOutsideDropletPresetState(
    double amount,
    double size,
    double strength,
    double seed,
    bool deformWithSurface,
    bool fallEnabled,
    double fallingRatio,
    double fallSpeed,
    double trailLength)
{
    var state = DetailedWipePresetFactory.Create(DetailedWipeBuiltInPreset.Heart);
    state.OutsideDropletAmount = amount;
    state.OutsideDropletSize = size;
    state.OutsideDropletStrength = strength;
    state.OutsideDropletSeed = seed;
    state.OutsideDropletDeformWithSurface = deformWithSurface;
    state.OutsideDropletFallEnabled = fallEnabled;
    state.OutsideDropletFallingRatio = fallingRatio;
    state.OutsideDropletFallSpeed = fallSpeed;
    state.OutsideDropletTrailLength = trailLength;
    return state;
}

static IEnumerable<string> OutsideDropletPresetPropertyNames()
{
    yield return "outsideDropletAmount";
    yield return "outsideDropletSize";
    yield return "outsideDropletStrength";
    yield return "outsideDropletSeed";
    yield return "outsideDropletDeformWithSurface";
    yield return "outsideDropletFallEnabled";
    yield return "outsideDropletFallingRatio";
    yield return "outsideDropletFallSpeed";
    yield return "outsideDropletTrailLength";
    yield return "outsideDropletAppearance";
}

static void OutsideDropletPresetStateEqual(
    DetailedWipePresetState expected,
    DetailedWipePresetState actual,
    string message)
{
    NearlyEqual(
        (float)(expected.OutsideDropletAmount ?? -1),
        (float)(actual.OutsideDropletAmount ?? -1),
        $"{message} 水滴量");
    NearlyEqual(
        (float)(expected.OutsideDropletSize ?? -1),
        (float)(actual.OutsideDropletSize ?? -1),
        $"{message} 水滴サイズ");
    NearlyEqual(
        (float)(expected.OutsideDropletStrength ?? -1),
        (float)(actual.OutsideDropletStrength ?? -1),
        $"{message} 水滴濃さ");
    NearlyEqual(
        (float)(expected.OutsideDropletSeed ?? -1),
        (float)(actual.OutsideDropletSeed ?? -1),
        $"{message} 水滴Seed");
    Equal(
        expected.OutsideDropletDeformWithSurface,
        actual.OutsideDropletDeformWithSurface,
        $"{message} 面変形");
    Equal(
        expected.OutsideDropletFallEnabled,
        actual.OutsideDropletFallEnabled,
        $"{message} 落下ON/OFF");
    NearlyEqual(
        (float)(expected.OutsideDropletFallingRatio ?? -1),
        (float)(actual.OutsideDropletFallingRatio ?? -1),
        $"{message} 落下割合");
    NearlyEqual(
        (float)(expected.OutsideDropletFallSpeed ?? -1),
        (float)(actual.OutsideDropletFallSpeed ?? -1),
        $"{message} 落下速度");
    NearlyEqual(
        (float)(expected.OutsideDropletTrailLength ?? -1),
        (float)(actual.OutsideDropletTrailLength ?? -1),
        $"{message} 水筋長さ");
    Equal(expected.OutsideDropletAppearance, actual.OutsideDropletAppearance,
        $"{message} 水滴の見た目");
}

static float ReferenceOutsideDropletLocalTimeSeconds(int frame, int fps) =>
    Math.Max(frame, 0) / (float)Math.Max(fps, 1);

static (
    float FallDuration,
    float VisibleWaitDuration,
    float HiddenWaitDuration,
    float CycleDuration,
    float PhaseOffset) GetOutsideDropletReferenceTimings(
        uint emitterKey,
        float speedScale,
        float sizeSpeedScale)
{
    var safeSpeed = float.IsFinite(speedScale)
        ? Math.Clamp(speedScale, 0.25f, 4)
        : 1;
    var safeSizeSpeed = float.IsFinite(sizeSpeedScale)
        ? Math.Clamp(sizeSpeedScale, 0.8f, 1.25f)
        : 1;
    var fallDuration = 6f / (safeSpeed * safeSizeSpeed);
    var visibleWaitDuration = fallDuration * LerpReference(
        0.2f,
        0.45f,
        ReferenceOutsideDropletSample(emitterKey, 5));
    var hiddenWaitDuration = fallDuration * LerpReference(
        0.15f,
        0.45f,
        ReferenceOutsideDropletSample(emitterKey, 6));
    var cycleDuration = visibleWaitDuration + fallDuration + hiddenWaitDuration;
    var phaseOffset = ReferenceOutsideDropletSample(emitterKey, 7) * cycleDuration;
    return (
        fallDuration,
        visibleWaitDuration,
        hiddenWaitDuration,
        cycleDuration,
        phaseOffset);
}

static OutsideDropletLifecycle EvaluateOutsideDropletHlslReference(
    float localTimeSeconds,
    uint emitterKey,
    float speedScale,
    float sizeSpeedScale)
{
    var safeTime = float.IsFinite(localTimeSeconds)
        ? MathF.Max(localTimeSeconds, 0)
        : 0;
    var timings = GetOutsideDropletReferenceTimings(emitterKey, speedScale, sizeSpeedScale);
    var absoluteCycleTime = safeTime + timings.PhaseOffset;
    var cycleIndex = (long)MathF.Floor(absoluteCycleTime / timings.CycleDuration);
    var cycleTime = absoluteCycleTime - cycleIndex * timings.CycleDuration;
    if (cycleTime < timings.VisibleWaitDuration)
    {
        var waitProgress = Math.Clamp(cycleTime / timings.VisibleWaitDuration, 0, 1);
        return new OutsideDropletLifecycle(
            cycleIndex,
            0,
            SmoothStepReference(0, 0.1f, waitProgress),
            OutsideDropletPhase.WaitingAtStart);
    }

    var fallTime = cycleTime - timings.VisibleWaitDuration;
    if (fallTime < timings.FallDuration)
    {
        var progress = Math.Clamp(fallTime / timings.FallDuration, 0, 1);
        return new OutsideDropletLifecycle(
            cycleIndex,
            progress,
            1 - SmoothStepReference(0.92f, 1, progress),
            OutsideDropletPhase.Falling);
    }

    return new OutsideDropletLifecycle(
        cycleIndex,
        1,
        0,
        OutsideDropletPhase.Hidden);
}

static void OutsideDropletLifecycleEqual(
    OutsideDropletLifecycle expected,
    OutsideDropletLifecycle actual,
    string message)
{
    Equal(expected.CycleIndex, actual.CycleIndex, $"{message} CycleIndex");
    Equal(expected.Phase, actual.Phase, $"{message} Phase");
    NearlyEqual(expected.Progress, actual.Progress, $"{message} Progress", 0.00001f);
    NearlyEqual(expected.Visibility, actual.Visibility, $"{message} Visibility", 0.00001f);
}

static float ReferenceOutsideDropletSample(uint key, uint channel)
{
    unchecked
    {
        var bits = ReferenceOutsideDropletMix(
            key ^ ReferenceOutsideDropletMix(channel + 0x9e3779b9U));
        return (bits >> 8) * (1.0f / 16777216.0f);
    }
}

static uint ReferenceOutsideDropletMix(uint value)
{
    unchecked
    {
        value ^= value >> 16;
        value *= 0x7feb352dU;
        value ^= value >> 15;
        value *= 0x846ca68bU;
        value ^= value >> 16;
        return value;
    }
}

static float SmoothStepReference(float minimum, float maximum, float value)
{
    var amount = Math.Clamp((value - minimum) / (maximum - minimum), 0, 1);
    return amount * amount * (3 - 2 * amount);
}

static float LerpReference(float from, float to, float amount) =>
    from + (to - from) * amount;

static string ExtractHlslFunction(string source, string signature)
{
    var start = source.IndexOf(signature, StringComparison.Ordinal);
    if (start < 0)
    {
        throw new InvalidOperationException($"HLSL関数が見つかりません: {signature}");
    }

    var openingBrace = source.IndexOf('{', start);
    if (openingBrace < 0)
    {
        throw new InvalidOperationException($"HLSL関数の開始括弧が見つかりません: {signature}");
    }

    var depth = 0;
    for (var index = openingBrace; index < source.Length; index++)
    {
        if (source[index] == '{')
        {
            depth++;
        }
        else if (source[index] == '}')
        {
            depth--;
            if (depth == 0)
            {
                return source[start..(index + 1)];
            }
        }
    }

    throw new InvalidOperationException($"HLSL関数の終了括弧が見つかりません: {signature}");
}

static (Vector3 Color, float Coverage) ReferenceCompositeOutsideDropletSurface(
    Vector3 sceneColor,
    Vector3 dropletLighting,
    float influence,
    float surfaceWeight,
    float highlightWeight,
    float shadowWeight)
{
    var safeInfluence = Math.Clamp(influence, 0, 1);
    var coverage = Math.Clamp(dropletLighting.Z * safeInfluence, 0, 1);
    var stableSurfaceColor = new Vector3(0.62f, 0.68f, 0.72f);
    var result = Vector3.Lerp(
        sceneColor,
        stableSurfaceColor,
        coverage * surfaceWeight);
    result = Vector3.Lerp(
        result,
        Vector3.One,
        Math.Clamp(dropletLighting.X * safeInfluence * highlightWeight, 0, 1));
    result = Vector3.Lerp(
        result,
        Vector3.Zero,
        Math.Clamp(dropletLighting.Y * safeInfluence * shadowWeight, 0, 1));
    return (Vector3.Clamp(result, Vector3.Zero, Vector3.One), coverage);
}

static Vector2 ReferenceFallingDropletWidthProfile(
    float normalizedY,
    float fallProgress)
{
    var morph = SmoothStepReference(0.05f, 0.35f, Math.Clamp(fallProgress, 0, 1));
    var height = Math.Clamp(normalizedY * 0.5f + 0.5f, 0, 1);
    var tipBlend = SmoothStepReference(0, 0.2f, height);
    var taperedWidth = 0.065f + height * 1.44f + (1 - tipBlend) * 0.10f;
    var tipParameter = Math.Clamp(height / 0.2f, 0, 1);
    var taperedSlope = 0.72f - 1.50f * tipParameter * (1 - tipParameter);
    var insideShape = normalizedY >= -0.9999f && normalizedY < 0.9999f ? 1 : 0;
    return new Vector2(
        LerpReference(1, taperedWidth, morph),
        taperedSlope * morph * insideShape);
}

static float ReferenceFallingDropletWidthScale(
    float normalizedY,
    float fallProgress) =>
    ReferenceFallingDropletWidthProfile(normalizedY, fallProgress).X;

static float ReferenceFallingDropletHalfWidth(
    float normalizedY,
    float fallProgress) =>
    ReferenceFallingDropletWidthScale(normalizedY, fallProgress) *
    MathF.Sqrt(MathF.Max(1 - normalizedY * normalizedY, 0));

static float ReferenceFallingDropletDistance(
    Vector2 normalizedOffset,
    float fallProgress)
{
    var widthScale = Math.Max(
        ReferenceFallingDropletWidthScale(normalizedOffset.Y, fallProgress),
        0.02f);
    return new Vector2(
        normalizedOffset.X / widthScale,
        normalizedOffset.Y).Length();
}

static float ReferenceFallingDropletEdgeWidth(
    Vector2 normalizedOffset,
    float fallProgress,
    float dropletRadius,
    float verticalScale,
    float sourcePixelStep)
{
    var widthProfile = ReferenceFallingDropletWidthProfile(
        normalizedOffset.Y,
        fallProgress);
    var widthScale = MathF.Max(widthProfile.X, 0.02f);
    var shapedOffset = new Vector2(
        normalizedOffset.X / widthScale,
        normalizedOffset.Y);
    var dropletDistance = shapedOffset.Length();
    var direction = shapedOffset / MathF.Max(dropletDistance, 0.0001f);
    var normalizedGradientX = direction.X / widthScale;
    var normalizedGradientY =
        (direction.Y - direction.X * direction.X * widthProfile.Y / widthScale) /
        MathF.Max(verticalScale, 0.001f);
    return MathF.Max(
        sourcePixelStep * new Vector2(
            normalizedGradientX,
            normalizedGradientY).Length() /
            MathF.Max(dropletRadius, 0.001f),
        0.001f);
}

static float ReferenceOutsideDropletRegionPixelsPerInputPixel(
    GlassWipeHomography inputToLocal,
    Vector2 inputUv,
    Vector2 inputSize,
    Vector2 regionPixelSize)
{
    var numeratorU = inputToLocal.M11 * inputUv.X +
        inputToLocal.M12 * inputUv.Y + inputToLocal.M13;
    var numeratorV = inputToLocal.M21 * inputUv.X +
        inputToLocal.M22 * inputUv.Y + inputToLocal.M23;
    var denominator = inputToLocal.M31 * inputUv.X +
        inputToLocal.M32 * inputUv.Y + inputToLocal.M33;
    if (MathF.Abs(denominator) < 0.000001f)
    {
        return 1;
    }

    var denominatorSquared = MathF.Max(
        denominator * denominator,
        0.000000000001f);
    var gradientU = new Vector2(
        (inputToLocal.M11 * denominator - inputToLocal.M31 * numeratorU) /
            denominatorSquared / inputSize.X,
        (inputToLocal.M12 * denominator - inputToLocal.M32 * numeratorU) /
            denominatorSquared / inputSize.Y);
    var gradientV = new Vector2(
        (inputToLocal.M21 * denominator - inputToLocal.M31 * numeratorV) /
            denominatorSquared / inputSize.X,
        (inputToLocal.M22 * denominator - inputToLocal.M32 * numeratorV) /
            denominatorSquared / inputSize.Y);
    var stepX = new Vector2(
        gradientU.X * regionPixelSize.X,
        gradientV.X * regionPixelSize.Y);
    var stepY = new Vector2(
        gradientU.Y * regionPixelSize.X,
        gradientV.Y * regionPixelSize.Y);
    return MathF.Max(MathF.Max(stepX.Length(), stepY.Length()), 0.001f);
}

static float ReferenceOutsideDropletRegionPixelsPerInputPixelFiniteDifference(
    GlassWipeHomography inputToLocal,
    Vector2 inputUv,
    Vector2 inputSize,
    Vector2 regionPixelSize)
{
    var halfPixelX = new Vector2(0.5f / inputSize.X, 0);
    var halfPixelY = new Vector2(0, 0.5f / inputSize.Y);
    var stepX = (
        inputToLocal.Transform(inputUv + halfPixelX) -
        inputToLocal.Transform(inputUv - halfPixelX)) * regionPixelSize;
    var stepY = (
        inputToLocal.Transform(inputUv + halfPixelY) -
        inputToLocal.Transform(inputUv - halfPixelY)) * regionPixelSize;
    return MathF.Max(MathF.Max(stepX.Length(), stepY.Length()), 0.001f);
}

static float ReferenceOutsideDropletUpperAnchorY(
    float headCenterY,
    float radius,
    float verticalScale,
    float fallProgress) =>
    headCenterY -
    radius * verticalScale *
    SmoothStepReference(0.05f, 0.35f, fallProgress) *
    0.65f;

static string RemoveWhitespace(string value) =>
    new string(value.Where(character => !char.IsWhiteSpace(character)).ToArray());

static Vector2 NormalizeScenePosition(
    Vector2 scenePosition,
    Vector2 inputOrigin,
    Vector2 inputSize) =>
    (scenePosition - inputOrigin) / inputSize;

static Vector2 ToShortSideVector(
    Vector2 maskPixelVector,
    WipeMaskGeometry geometry) =>
    new(
        maskPixelVector.X / WipeMaskRenderer.MaskSize / geometry.ScaleX,
        maskPixelVector.Y / WipeMaskRenderer.MaskSize / geometry.ScaleY);

static bool MatrixIsFinite(Matrix3x2 matrix) =>
    float.IsFinite(matrix.M11) &&
    float.IsFinite(matrix.M12) &&
    float.IsFinite(matrix.M21) &&
    float.IsFinite(matrix.M22) &&
    float.IsFinite(matrix.M31) &&
    float.IsFinite(matrix.M32);

static Vector2 RegionLocalToInput(
    Vector2 regionPosition,
    Vector2 centerPixels,
    Vector2 sizePixels,
    float rotationCos,
    float rotationSin)
{
    var localPixelPosition = (regionPosition - new Vector2(0.5f)) * sizePixels;
    var rotatedPixelPosition = new Vector2(
        rotationCos * localPixelPosition.X - rotationSin * localPixelPosition.Y,
        rotationSin * localPixelPosition.X + rotationCos * localPixelPosition.Y);
    return centerPixels + rotatedPixelPosition;
}

static Vector2 InputToRegionLocal(
    Vector2 inputPixelPosition,
    Vector2 centerPixels,
    Vector2 sizePixels,
    float rotationCos,
    float rotationSin)
{
    var offsetPixels = inputPixelPosition - centerPixels;
    var localPixelPosition = new Vector2(
        rotationCos * offsetPixels.X + rotationSin * offsetPixels.Y,
        -rotationSin * offsetPixels.X + rotationCos * offsetPixels.Y);
    return localPixelPosition / sizePixels + new Vector2(0.5f);
}

static float RegionMaskReference(
    GlassWipeRegionShape regionShape,
    Vector2 localPixelPosition,
    Vector2 halfSizePixels,
    float featherPixels)
{
    if (halfSizePixels.X <= 0 || halfSizePixels.Y <= 0)
    {
        return 0;
    }

    if (regionShape == GlassWipeRegionShape.FullScreen)
    {
        return 1;
    }

    float distanceInsidePixels;
    if (regionShape == GlassWipeRegionShape.Rectangle)
    {
        var distanceFromCenterPixels = Vector2.Abs(localPixelPosition);
        distanceInsidePixels = MathF.Min(
            halfSizePixels.X - distanceFromCenterPixels.X,
            halfSizePixels.Y - distanceFromCenterPixels.Y);
    }
    else
    {
        var normalizedPosition = localPixelPosition / halfSizePixels;
        var ellipseDistanceSquared = Vector2.Dot(
            normalizedPosition,
            normalizedPosition);
        var ellipseGradient = 2 * localPixelPosition /
            (halfSizePixels * halfSizePixels);
        var gradientLength = ellipseGradient.Length();
        distanceInsidePixels = gradientLength > 0.000001f
            ? (1 - ellipseDistanceSquared) / gradientLength
            : MathF.Min(halfSizePixels.X, halfSizePixels.Y);
    }

    if (featherPixels <= 0)
    {
        return distanceInsidePixels >= 0 ? 1 : 0;
    }

    return Math.Clamp(distanceInsidePixels / featherPixels, 0, 1);
}

static float QuadFeatherReference(
    GlassWipeHomography inputToLocal,
    Vector2 inputUv,
    Vector2 inputSize,
    float featherPixels)
{
    var numeratorU = inputToLocal.M11 * inputUv.X +
        inputToLocal.M12 * inputUv.Y + inputToLocal.M13;
    var numeratorV = inputToLocal.M21 * inputUv.X +
        inputToLocal.M22 * inputUv.Y + inputToLocal.M23;
    var denominator = inputToLocal.M31 * inputUv.X +
        inputToLocal.M32 * inputUv.Y + inputToLocal.M33;
    var denominatorSquared = denominator * denominator;
    var gradientU = new Vector2(
        (inputToLocal.M11 * denominator - inputToLocal.M31 * numeratorU) /
            denominatorSquared / inputSize.X,
        (inputToLocal.M12 * denominator - inputToLocal.M32 * numeratorU) /
            denominatorSquared / inputSize.Y);
    var gradientV = new Vector2(
        (inputToLocal.M21 * denominator - inputToLocal.M31 * numeratorV) /
            denominatorSquared / inputSize.X,
        (inputToLocal.M22 * denominator - inputToLocal.M32 * numeratorV) /
            denominatorSquared / inputSize.Y);
    var regionUv = new Vector2(numeratorU, numeratorV) / denominator;
    var localDistance = Vector2.Min(regionUv, Vector2.One - regionUv);
    var distanceInsidePixels = MathF.Min(
        localDistance.X / MathF.Max(gradientU.Length(), 0.000001f),
        localDistance.Y / MathF.Max(gradientV.Length(), 0.000001f));

    if (featherPixels <= 0)
    {
        return distanceInsidePixels >= 0 ? 1 : 0;
    }

    return Math.Clamp(distanceInsidePixels / featherPixels, 0, 1);
}
static int OffsetOf(string fieldName) =>
    Marshal.OffsetOf<GlassCompositeConstants>(fieldName).ToInt32();

static void VectorEqual(Vector4 expected, Vector4 actual, string message)
{
    NearlyEqual(expected.X, actual.X, message);
    NearlyEqual(expected.Y, actual.Y, message);
    NearlyEqual(expected.Z, actual.Z, message);
    NearlyEqual(expected.W, actual.W, message);
}

static void Vector2Equal(
    Vector2 expected,
    Vector2 actual,
    string message,
    float tolerance = 0.000001f)
{
    NearlyEqual(expected.X, actual.X, message, tolerance);
    NearlyEqual(expected.Y, actual.Y, message, tolerance);
}

static void NearlyEqual(
    float expected,
    float actual,
    string message,
    float tolerance = 0.000001f)
{
    if (MathF.Abs(expected - actual) > tolerance)
    {
        throw new InvalidOperationException(
            $"{message} Expected={expected}, Actual={actual}");
    }
}

static void Equal<T>(T expected, T actual, string message)
{
    if (!EqualityComparer<T>.Default.Equals(expected, actual))
    {
        throw new InvalidOperationException(
            $"{message} Expected={expected}, Actual={actual}");
    }
}

static void True(bool condition, string message)
{
    if (!condition)
    {
        throw new InvalidOperationException(message);
    }
}

internal sealed class BoundedStampCollector : ICollection<WipeBrushStamp>
{
    private readonly int _capacity;
    private readonly List<WipeBrushStamp> _buffer;
    private readonly List<WipeBrushStamp> _items = [];

    public BoundedStampCollector(int capacity)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(capacity);
        _capacity = capacity;
        _buffer = new List<WipeBrushStamp>(capacity);
    }

    public IReadOnlyList<WipeBrushStamp> Items => _items;

    public int MaximumBufferedCount { get; private set; }

    public int Count => _items.Count + _buffer.Count;

    public bool IsReadOnly => false;

    public void Add(WipeBrushStamp item)
    {
        _buffer.Add(item);
        MaximumBufferedCount = Math.Max(MaximumBufferedCount, _buffer.Count);
        if (_buffer.Count >= _capacity)
        {
            Flush();
        }
    }

    public void Flush()
    {
        _items.AddRange(_buffer);
        _buffer.Clear();
    }

    public void Clear()
    {
        _items.Clear();
        _buffer.Clear();
        MaximumBufferedCount = 0;
    }

    public bool Contains(WipeBrushStamp item) =>
        _items.Contains(item) || _buffer.Contains(item);

    public void CopyTo(WipeBrushStamp[] array, int arrayIndex)
    {
        _items.CopyTo(array, arrayIndex);
        _buffer.CopyTo(array, arrayIndex + _items.Count);
    }

    public bool Remove(WipeBrushStamp item) =>
        _buffer.Remove(item) || _items.Remove(item);

    public IEnumerator<WipeBrushStamp> GetEnumerator() =>
        _items.Concat(_buffer).GetEnumerator();

    System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() =>
        GetEnumerator();
}
