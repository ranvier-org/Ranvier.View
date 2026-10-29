# Native API audit source ledger

Companion evidence for [the candidate checklist](2026-10-29-native-api-candidates.md). Generated from the pinned local WebCore IDL corpus; this is an inventory, not a generated binding or a runtime support report.

## Provenance

- WebCore root: `C:/wk/src/Source/WebCore`, commit `b95c91b1c0ee43cc86d25ff7af73038f67a88e04` (local patched checkout; the ABI generator patch adds native glue, not a replacement API corpus).
- ABI model: `C:/wk/native-gap-interop06/gen/dom-api.json`; source baseline `eef227e280664a10e3af75a7d5c75dc218b37a23`.
- Legacy ABI header: `C:/wk/native-gap-interop06/host/RanvierWebCoreAbi.h`; generated ABI header: `host/RanvierWebCoreDom.h`.
- Native omissions: `C:/wk/native-gap-interop06/gen/dom-report.txt`; managed adapter omissions: `C:/wk/ranvier-views/src/Ranvier.View.WebCore/Generated/report.txt`.
- Build policy: native repo `config/{common,slim,gpu,mac,wpe}.cmake` and `docs/SETTINGS.md`; individual source conditionals/settings are not proof of enabled features.

## Coverage and interpretation

- 1811 IDL files; 1158 interface declarations; 6069 parsed non-constant member declarations; zero parse errors.
- Excluded directories are exact path components `test`, `tests`, `testing`; no Modules family was excluded.
- Every file below has one or more candidate-group references. Mapping to a group does not mean every member of that file is missing or desirable; existing operations share interfaces with missing extensions.
- Constructors, partials, mixins, helper dictionaries/enums/callbacks and namespace source are included. Interface member names are evidence, not copied implementations.
- The lightweight parser skips namespace bodies and iterable/maplike/setlike statements; those raw declarations are separately shown below, so their APIs are not silently lost.
- Existing function records are listed at the end by interface and kind. They must be combined with the legacy ABI and host adapter capabilities before declaring a particular operation missing.
- This ledger does not certify compiled support, non-JS callback feasibility or platform parity.

## Complete source-file inventory

### accessibility

- `accessibility/AccessibilityRole.idl` **[I10, I11]** - AccessibilityRole (mixin): role.
- `accessibility/AriaAttributes.idl` **[I10, I11]** - AriaAttributes (mixin): ariaActiveDescendantElement, ariaActionsElements, ariaAtomic, ariaAutoComplete, ariaBrailleLabel, ariaBrailleRoleDescription, ariaBusy, ariaChecked, ariaColCount, ariaColIndex, ariaColIndexText, ariaColSpan, ariaControlsElements, ariaCurrent, ariaDescribedByElements, ariaDescription, ariaDetailsElements, ariaDisabled, ariaErrorMessageElements, ariaExpanded, ariaFlowToElements, ariaHasPopup, ariaHidden, ariaInvalid, ariaKeyShortcuts, ariaLabel, ariaLabelledByElements, ariaLevel, ariaLive, ariaModal, ariaMultiLine, ariaMultiSelectable, ariaOrientation, ariaOwnsElements, ariaPlaceholder, ariaPosInSet, ariaPressed, ariaReadOnly, ariaRelevant, ariaRequired, ariaRoleDescription, ariaRowCount, ariaRowIndex, ariaRowIndexText, ariaRowSpan, ariaSelected, ariaSetSize, ariaSort, ariaValueMax, ariaValueMin, ariaValueNow, ariaValueText.
### animation

- `animation/Animatable.idl` **[A01, A03, A04, A05]** - Animatable (mixin): animate, getAnimations.
- `animation/AnimationEffect.idl` **[A01, A02]** - AnimationEffect: getTiming, getComputedTiming, updateTiming.
- `animation/AnimationFrameProvider.idl` **[A12]** - AnimationFrameProvider (mixin): requestAnimationFrame, cancelAnimationFrame.
- `animation/AnimationFrameRatePreset.idl` **[A01, A03, A04, A05]** - enums: AnimationFrameRatePreset.
- `animation/AnimationPlaybackEvent.idl` **[A10]** - AnimationPlaybackEvent: constructor, currentTime, timelineTime.
- `animation/AnimationPlaybackEventInit.idl` **[A10]** - dictionaries: AnimationPlaybackEventInit.
- `animation/AnimationTimeline.idl` **[A06]** - AnimationTimeline: currentTime, duration.
- `animation/CompositeOperation.idl` **[A01, A03, A04, A05]** - enums: CompositeOperation.
- `animation/CompositeOperationOrAuto.idl` **[A01, A03, A04, A05]** - enums: CompositeOperationOrAuto.
- `animation/ComputedEffectTiming.idl` **[A01, A02]** - dictionaries: ComputedEffectTiming.
- `animation/CSSAnimation.idl` **[A01, A03, A04, A05]** - CSSAnimation: animationName.
- `animation/CSSAnimationEvent.idl` **[A10]** - CSSAnimationEvent: constructor, animation, animationName, elapsedTime, pseudoElement; dictionaries: AnimationEventInit.
- `animation/CSSTransition.idl` **[A01, A03, A04, A05]** - CSSTransition: transitionProperty.
- `animation/CSSTransitionEvent.idl` **[A10]** - CSSTransitionEvent: constructor, animation, propertyName, elapsedTime, pseudoElement; dictionaries: TransitionEventInit.
- `animation/CustomAnimationOptions.idl` **[A09]** - dictionaries: CustomAnimationOptions.
- `animation/CustomEffect.idl` **[A09]** - CustomEffect: constructor.
- `animation/CustomEffectCallback.idl` **[A09]** - callbacks: CustomEffectCallback.
- `animation/Document+WebAnimations.idl` **[A01, A03, A04, A05]** - Document (partial): timeline.
- `animation/DocumentOrShadowRoot+WebAnimations.idl` **[A01, A03, A04, A05]** - DocumentOrShadowRoot (partial) (mixin): getAnimations.
- `animation/DocumentTimeline.idl` **[A06]** - DocumentTimeline: constructor, animate, maximumFrameRate.
- `animation/DocumentTimelineOptions.idl` **[A06]** - dictionaries: DocumentTimelineOptions.
- `animation/EffectTiming.idl` **[A01, A02]** - dictionaries: EffectTiming.
- `animation/FillMode.idl` **[A01, A03, A04, A05]** - enums: FillMode.
- `animation/GetAnimationsOptions.idl` **[A01, A03, A04, A05]** - dictionaries: GetAnimationsOptions.
- `animation/GlobalEventHandlers+CSSAnimations.idl` **[A10]** - GlobalEventHandlers (partial) (mixin): onanimationstart, onanimationiteration, onanimationend, onanimationcancel.
- `animation/GlobalEventHandlers+CSSTransitions.idl` **[A10]** - GlobalEventHandlers (partial) (mixin): ontransitionrun, ontransitionstart, ontransitionend, ontransitioncancel.
- `animation/IterationCompositeOperation.idl` **[A01, A03, A04, A05]** - enums: IterationCompositeOperation.
- `animation/KeyframeAnimationOptions.idl` **[A01, A02]** - dictionaries: KeyframeAnimationOptions.
- `animation/KeyframeEffect.idl` **[A01, A02]** - KeyframeEffect: constructor, target, pseudoElement, iterationComposite, composite, getKeyframes, setKeyframes; dictionaries: BaseComputedKeyframe, BaseKeyframe, BasePropertyIndexedKeyframe.
- `animation/KeyframeEffectOptions.idl` **[A01, A02]** - dictionaries: KeyframeEffectOptions.
- `animation/OptionalEffectTiming.idl` **[A01, A02]** - dictionaries: OptionalEffectTiming.
- `animation/PlaybackDirection.idl` **[A01, A03, A04, A05]** - enums: PlaybackDirection.
- `animation/ScrollAxis.idl` **[A07]** - enums: ScrollAxis.
- `animation/ScrollTimeline.idl` **[A07]** - ScrollTimeline: constructor, source, axis.
- `animation/ScrollTimelineOptions.idl` **[A07]** - dictionaries: ScrollTimelineOptions.
- `animation/TimelineRangeOffset.idl` **[A08]** - dictionaries: TimelineRangeOffset.
- `animation/ViewTimeline.idl` **[A08]** - ViewTimeline: constructor, subject, startOffset, endOffset.
- `animation/ViewTimelineOptions.idl` **[A08]** - dictionaries: ViewTimelineOptions.
- `animation/WebAnimation.idl` **[A01, A03, A04, A05]** - WebAnimation: constructor, id, effect, timeline, startTime, currentTime, rangeStart, rangeEnd, playbackRate, overallProgress, frameRate, playState, replaceState, pending, onfinish, oncancel, onremove, ready, finished, cancel, finish, play, pause, updatePlaybackRate, reverse, persist, commitStyles; enums: AnimationPlayState, AnimationReplaceState.
### crypto

- `crypto/CryptoAlgorithmParameters.idl` **[B22]** - dictionaries: CryptoAlgorithmParameters.
- `crypto/CryptoKey.idl` **[B22]** - CryptoKey: type, extractable, algorithm, usages; enums: KeyType.
- `crypto/CryptoKeyPair.idl` **[B22]** - dictionaries: CryptoKeyPair.
- `crypto/CryptoKeyUsage.idl` **[B22]** - enums: CryptoKeyUsage.
- `crypto/JsonWebKey.idl` **[B22]** - dictionaries: JsonWebKey.
- `crypto/keys/CryptoAesKeyAlgorithm.idl` **[B22]** - dictionaries: CryptoAesKeyAlgorithm.
- `crypto/keys/CryptoEcKeyAlgorithm.idl` **[B22]** - dictionaries: CryptoEcKeyAlgorithm.
- `crypto/keys/CryptoHmacKeyAlgorithm.idl` **[B22]** - dictionaries: CryptoHmacKeyAlgorithm.
- `crypto/keys/CryptoKeyAlgorithm.idl` **[B22]** - dictionaries: CryptoKeyAlgorithm.
- `crypto/keys/CryptoRsaHashedKeyAlgorithm.idl` **[B22]** - dictionaries: CryptoRsaHashedKeyAlgorithm.
- `crypto/keys/CryptoRsaKeyAlgorithm.idl` **[B22]** - dictionaries: CryptoRsaKeyAlgorithm.
- `crypto/parameters/AesCbcCfbParams.idl` **[B22]** - dictionaries: AesCbcCfbParams.
- `crypto/parameters/AesCtrParams.idl` **[B22]** - dictionaries: AesCtrParams.
- `crypto/parameters/AesGcmParams.idl` **[B22]** - dictionaries: AesGcmParams.
- `crypto/parameters/AesKeyParams.idl` **[B22]** - dictionaries: AesKeyParams.
- `crypto/parameters/EcdhKeyDeriveParams.idl` **[B22]** - dictionaries: EcdhKeyDeriveParams.
- `crypto/parameters/EcdsaParams.idl` **[B22]** - dictionaries: EcdsaParams.
- `crypto/parameters/EcKeyParams.idl` **[B22]** - dictionaries: EcKeyParams.
- `crypto/parameters/HkdfParams.idl` **[B22]** - dictionaries: HkdfParams.
- `crypto/parameters/HmacKeyParams.idl` **[B22]** - dictionaries: HmacKeyParams.
- `crypto/parameters/Pbkdf2Params.idl` **[B22]** - dictionaries: Pbkdf2Params.
- `crypto/parameters/RsaHashedImportParams.idl` **[B22]** - dictionaries: RsaHashedImportParams.
- `crypto/parameters/RsaHashedKeyGenParams.idl` **[B22]** - dictionaries: RsaHashedKeyGenParams.
- `crypto/parameters/RsaKeyGenParams.idl` **[B22]** - dictionaries: RsaKeyGenParams.
- `crypto/parameters/RsaOaepParams.idl` **[B22]** - dictionaries: RsaOaepParams.
- `crypto/parameters/RsaPssParams.idl` **[B22]** - dictionaries: RsaPssParams.
- `crypto/parameters/X25519Params.idl` **[B22]** - dictionaries: X25519Params.
- `crypto/RsaOtherPrimesInfo.idl` **[B22]** - dictionaries: RsaOtherPrimesInfo.
- `crypto/SubtleCrypto.idl` **[B22]** - SubtleCrypto: encrypt, decrypt, sign, verify, digest, generateKey, deriveKey, deriveBits, importKey, exportKey, wrapKey, unwrapKey; enums: KeyFormat.
### css

- `css/CSSConditionRule.idl` **[C09]** - CSSConditionRule: conditionText.
- `css/CSSContainerRule.idl` **[C09]** - CSSContainerRule: containerName, containerQuery.
- `css/CSSCounterStyleRule.idl` **[C19]** - CSSCounterStyleRule: name, system, symbols, additiveSymbols, negative, prefix, suffix, range, pad, speakAs, fallback.
- `css/CSSFontFaceDescriptors.idl` **[C18]** - CSSFontFaceDescriptors: src, fontFamily, font-family, fontStyle, font-style, fontWeight, font-weight, fontStretch, font-stretch, fontWidth, font-width, sizeAdjust, size-adjust, unicodeRange, unicode-range, fontFeatureSettings, font-feature-settings, fontDisplay, font-display, ascentOverride, ascent-override, descentOverride, descent-override, lineGapOverride, line-gap-override.
- `css/CSSFontFaceRule.idl` **[C18]** - CSSFontFaceRule: style.
- `css/CSSFontFeatureValuesRule.idl` **[C18]** - CSSFontFeatureValuesRule: fontFamily.
- `css/CSSFontPaletteValuesRule.idl` **[C18]** - CSSFontPaletteValuesRule: name, fontFamily, basePalette, overrideColors.
- `css/CSSFunctionDeclarations.idl` **[C20]** - CSSFunctionDeclarations: style.
- `css/CSSFunctionDescriptors.idl` **[C20]** - CSSFunctionDescriptors: result.
- `css/CSSFunctionRule.idl` **[C20]** - CSSFunctionRule: name, getParameters, returnType; dictionaries: FunctionParameter.
- `css/CSSGroupingRule.idl` **[C08]** - CSSGroupingRule: cssRules, insertRule, deleteRule.
- `css/CSSImportRule.idl` **[C10]** - CSSImportRule: href, media, styleSheet, layerName, supportsText.
- `css/CSSKeyframeRule.idl` **[C11]** - CSSKeyframeRule: keyText, style.
- `css/CSSKeyframesRule.idl` **[C11]** - CSSKeyframesRule: name, cssRules, length, <indexed operation>, appendRule, deleteRule, findRule.
- `css/CSSLayerBlockRule.idl` **[C10]** - CSSLayerBlockRule: name.
- `css/CSSLayerStatementRule.idl` **[C10]** - CSSLayerStatementRule: nameList.
- `css/CSSMediaRule.idl` **[C09]** - CSSMediaRule: media.
- `css/CSSNamespaceRule.idl` **[C10]** - CSSNamespaceRule: namespaceURI, prefix.
- `css/CSSNestedDeclarations.idl` **[C10]** - CSSNestedDeclarations: style.
- `css/CSSPageDescriptors.idl` **[C23]** - CSSPageDescriptors: margin, marginTop, marginRight, marginBottom, marginLeft, margin-top, margin-right, margin-bottom, margin-left, size.
- `css/CSSPageRule.idl` **[C23]** - CSSPageRule: selectorText, style.
- `css/CSSPaintCallback.idl` **[B16]** - callbacks: CSSPaintCallback.
- `css/CSSPaintSize.idl` **[B16]** - CSSPaintSize: width, height.
- `css/CSSPositionTryDescriptors.idl` **[C21]** - CSSPositionTryDescriptors: margin, marginTop, marginRight, marginBottom, marginLeft, marginBlock, marginBlockStart, marginBlockEnd, marginInline, marginInlineStart, marginInlineEnd, margin-top, margin-right, margin-bottom, margin-left, margin-block, margin-block-start, margin-block-end, margin-inline, margin-inline-start, margin-inline-end, inset, insetBlock, insetBlockStart, insetBlockEnd, insetInline, insetInlineStart, insetInlineEnd, top, left, right, bottom, inset-block, inset-block-start, inset-block-end, inset-inline, inset-inline-start, inset-inline-end, width, minWidth, maxWidth, height, minHeight, maxHeight, blockSize, minBlockSize, maxBlockSize, inlineSize, minInlineSize, maxInlineSize, min-width, max-width, min-height, max-height, block-size, min-block-size, max-block-size, inline-size, min-inline-size, max-inline-size, placeSelf, alignSelf, justifySelf, place-self, align-self, justify-self, positionAnchor, position-anchor, positionArea, position-area.
- `css/CSSPositionTryRule.idl` **[C21]** - CSSPositionTryRule: name, style.
- `css/CSSPropertyRule.idl` **[C12]** - CSSPropertyRule: name, syntax, inherits, initialValue.
- `css/CSSRule.idl` **[C08]** - CSSRule: cssText, parentRule, parentStyleSheet, type.
- `css/CSSRuleList.idl` **[C08]** - CSSRuleList: item, length.
- `css/CSSScopeRule.idl` **[C10]** - CSSScopeRule: start, end.
- `css/CSSStartingStyleRule.idl` **[C22]** - CSSStartingStyleRule: no own parsed members.
- `css/CSSStyleDeclaration.idl` **[C04, D09]** - CSSStyleDeclaration: cssText, cssRules, length, item, getPropertyValue, getPropertyPriority, setProperty, removeProperty, parentRule, getPropertyCSSValue.
- `css/CSSStyleProperties.idl` **[C04, D09]** - CSSStyleProperties: cssFloat.
- `css/CSSStyleRule+Typedom.idl` **[C13]** - CSSStyleRule (partial): styleMap.
- `css/CSSStyleRule.idl` **[C08]** - CSSStyleRule: selectorText, style, cssRules, insertRule, deleteRule.
- `css/CSSStyleSheet.idl` **[C07, C08]** - CSSStyleSheet: constructor, ownerRule, cssRules, insertRule, deleteRule, replace, replaceSync, rules, addRule, removeRule; dictionaries: CSSStyleSheetInit.
- `css/CSSSupportsRule.idl` **[C09]** - CSSSupportsRule: no own parsed members.
- `css/CSSViewTransitionRule.idl` **[C22]** - CSSViewTransitionRule: navigation, types.
- `css/deprecated-cssom/DeprecatedCSSOMCounter.idl` **[C24]** - DeprecatedCSSOMCounter: identifier, listStyle, separator.
- `css/deprecated-cssom/DeprecatedCSSOMPrimitiveValue.idl` **[C24]** - DeprecatedCSSOMPrimitiveValue: primitiveType, setFloatValue, getFloatValue, setStringValue, getStringValue, getCounterValue, getRectValue, getRGBColorValue.
- `css/deprecated-cssom/DeprecatedCSSOMRect.idl` **[C24]** - DeprecatedCSSOMRect: top, right, bottom, left.
- `css/deprecated-cssom/DeprecatedCSSOMRGBColor.idl` **[C24]** - DeprecatedCSSOMRGBColor: red, green, blue, alpha.
- `css/deprecated-cssom/DeprecatedCSSOMValue.idl` **[C24]** - DeprecatedCSSOMValue: cssText, cssValueType.
- `css/deprecated-cssom/DeprecatedCSSOMValueList.idl` **[C24]** - DeprecatedCSSOMValueList: length, item.
- `css/DOMCSSCustomPropertyDescriptor.idl` **[C12]** - dictionaries: DOMCSSCustomPropertyDescriptor.
- `css/DOMCSSNamespace+CSSNumericFactory.idl` **[C15]** - namespace statements: CSSUnitValue number(double value); CSSUnitValue percent(double value); CSSUnitValue cap(double value); CSSUnitValue ch(double value); CSSUnitValue em(double value); CSSUnitValue ex(double value); CSSUnitValue ic(double value); CSSUnitValue lh(double value); CSSUnitValue rcap(double value); CSSUnitValue rch(double value); CSSUnitValue rem(double value); CSSUnitValue rex(double value); CSSUnitValue ric(double value); CSSUnitValue rlh(double value); CSSUnitValue vw(double value); CSSUnitValue vh(double value); CSSUnitValue vi(double value); CSSUnitValue vb(double value); CSSUnitValue vmin(double value); CSSUnitValue vmax(double value); CSSUnitValue svw(double value); CSSUnitValue svh(double value); CSSUnitValue svi(double value); CSSUnitValue svb(double value); CSSUnitValue svmin(double value); CSSUnitValue svmax(double value); CSSUnitValue lvw(double value); CSSUnitValue lvh(double value); CSSUnitValue lvi(double value); CSSUnitValue lvb(double value); CSSUnitValue lvmin(double value); CSSUnitValue lvmax(double value); CSSUnitValue dvw(double value); CSSUnitValue dvh(double value); CSSUnitValue dvi(double value); CSSUnitValue dvb(double value); CSSUnitValue dvmin(double value); CSSUnitValue dvmax(double value); CSSUnitValue cqw(double value); CSSUnitValue cqh(double value); CSSUnitValue cqi(double value); CSSUnitValue cqb(double value); CSSUnitValue cqmin(double value); CSSUnitValue cqmax(double value); CSSUnitValue cm(double value); CSSUnitValue mm(double value); CSSUnitValue Q(double value); CSSUnitValue in(double value); CSSUnitValue pt(double value); CSSUnitValue pc(double value); CSSUnitValue px(double value); CSSUnitValue deg(double value); CSSUnitValue grad(double value); CSSUnitValue rad(double value); CSSUnitValue turn(double value); CSSUnitValue s(double value); CSSUnitValue ms(double value); CSSUnitValue Hz(double value); CSSUnitValue kHz(double value); CSSUnitValue dpi(double value); CSSUnitValue dpcm(double value); CSSUnitValue dppx(double value); CSSUnitValue fr(double value);.
- `css/DOMCSSNamespace+CSSPainting.idl` **[B16]** - namespace statements: [ImplementedAs=ensurePaintWorklet, CallWith=CurrentDocument] readonly attribute Worklet paintWorklet;.
- `css/DOMCSSNamespace+CSSPropertiesandValues.idl` **[C12]** - namespace statements: [CallWith=CurrentDocument] undefined registerProperty(DOMCSSCustomPropertyDescriptor descriptor);.
- `css/DOMCSSNamespace.idl` **[C03, C05, T03]** - namespace statements: [CallWith=CurrentDocument] boolean supports(DOMString property, DOMString value); [CallWith=CurrentDocument] boolean supports(DOMString conditionText); DOMString escape(DOMString ident); [CallWith=CurrentDocument] readonly attribute HighlightRegistry highlights;.
- `css/DOMMatrix.idl` **[G08]** - DOMMatrix: constructor, fromMatrix, fromFloat32Array, fromFloat64Array, a, b, c, d, e, f, m11, m12, m13, m14, m21, m22, m23, m24, m31, m32, m33, m34, m41, m42, m43, m44, multiplySelf, preMultiplySelf, translateSelf, scaleSelf, scale3dSelf, rotateSelf, rotateFromVectorSelf, rotateAxisAngleSelf, skewXSelf, skewYSelf, invertSelf, setMatrixValue.
- `css/DOMMatrix2DInit.idl` **[G08]** - dictionaries: DOMMatrix2DInit.
- `css/DOMMatrixInit.idl` **[G08]** - dictionaries: DOMMatrixInit.
- `css/DOMMatrixReadOnly.idl` **[G08]** - DOMMatrixReadOnly: constructor, fromMatrix, fromFloat32Array, fromFloat64Array, a, b, c, d, e, f, m11, m12, m13, m14, m21, m22, m23, m24, m31, m32, m33, m34, m41, m42, m43, m44, is2D, isIdentity, translate, scale, scale3d, scaleNonUniform, rotate, rotateFromVector, rotateAxisAngle, skewX, skewY, multiply, flipX, flipY, inverse, transformPoint, toFloat32Array, toFloat64Array, toJSON.
- `css/ElementCSSInlineStyle+Typedom.idl` **[C13]** - ElementCSSInlineStyle (partial) (mixin): attributeStyleMap.
- `css/ElementCSSInlineStyle.idl` **[C04, D09]** - ElementCSSInlineStyle (mixin): style.
- `css/FontFace.idl` **[T01, T02]** - FontFace: constructor, family, style, weight, width, stretch, unicodeRange, featureSettings, display, ascentOverride, descentOverride, lineGapOverride, sizeAdjust, status, load, loaded; dictionaries: FontFaceDescriptors; enums: FontFaceLoadStatus.
- `css/FontFaceSet.idl` **[T01, T02]** - FontFaceSet: constructor, has, size, add, delete, clear, onloading, onloadingdone, onloadingerror, load, check, ready, status; enums: FontFaceSetLoadStatus; collections: iterable<FontFace>;.
- `css/FontFaceSource.idl` **[T01, T02]** - FontFaceSource (mixin): fonts.
- `css/LinkStyle.idl` **[C06, D12]** - LinkStyle (mixin): sheet.
- `css/MediaList.idl` **[C09]** - MediaList: mediaText, length, item, deleteMedium, appendMedium.
- `css/MediaQueryList.idl` **[G01]** - MediaQueryList: media, matches, addListener, removeListener, onchange.
- `css/MediaQueryListEvent.idl` **[G01]** - MediaQueryListEvent: constructor, media, matches; dictionaries: MediaQueryListEventInit.
- `css/StyleMedia.idl` **[B23]** - StyleMedia: type.
- `css/StyleSheet.idl` **[C06, D12]** - StyleSheet: type, href, ownerNode, parentStyleSheet, title, media, disabled.
- `css/StyleSheetList.idl` **[C06, D12]** - StyleSheetList: item, length, <indexed operation>.
- `css/typedom/color/CSSHSL.idl` **[C17]** - CSSHSL: constructor, h, s, l, alpha.
- `css/typedom/color/CSSHWB.idl` **[C17]** - CSSHWB: constructor, h, w, b, alpha.
- `css/typedom/color/CSSLab.idl` **[C17]** - CSSLab: constructor, l, a, b, alpha.
- `css/typedom/color/CSSLCH.idl` **[C17]** - CSSLCH: constructor, l, c, h, alpha.
- `css/typedom/color/CSSOKLab.idl` **[C17]** - CSSOKLab: constructor, l, a, b, alpha.
- `css/typedom/color/CSSOKLCH.idl` **[C17]** - CSSOKLCH: constructor, l, c, h, alpha.
- `css/typedom/color/CSSOMColor.idl` **[C17]** - CSSOMColor: constructor, colorSpace, alpha.
- `css/typedom/color/CSSOMColorValue.idl` **[C17]** - CSSOMColorValue: colorSpace, to, parse.
- `css/typedom/color/CSSRGB.idl` **[C17]** - CSSRGB: constructor, r, g, b, alpha.
- `css/typedom/CSSNumericValue.idl` **[C15]** - CSSNumericValue: add, sub, mul, div, min, max, equals, to, toSum, type, parse.
- `css/typedom/CSSOMKeywordValue.idl` **[C14]** - CSSOMKeywordValue: constructor, value.
- `css/typedom/CSSOMVariableReferenceValue.idl` **[C14]** - CSSOMVariableReferenceValue: constructor, variable, fallback.
- `css/typedom/CSSStyleImageValue.idl` **[C14]** - CSSStyleImageValue: no own parsed members.
- `css/typedom/CSSStyleValue.idl` **[C14]** - CSSStyleValue: parse, parseAll.
- `css/typedom/CSSUnitValue.idl` **[C15]** - CSSUnitValue: constructor, value, unit.
- `css/typedom/CSSUnparsedValue.idl` **[C14]** - CSSUnparsedValue: constructor, length, <indexed operation>; collections: iterable<CSSUnparsedSegment>;.
- `css/typedom/numeric/CSSMathClamp.idl` **[C15]** - CSSMathClamp: constructor, lower, value, upper.
- `css/typedom/numeric/CSSMathInvert.idl` **[C15]** - CSSMathInvert: constructor, value.
- `css/typedom/numeric/CSSMathMax.idl` **[C15]** - CSSMathMax: constructor, values.
- `css/typedom/numeric/CSSMathMin.idl` **[C15]** - CSSMathMin: constructor, values.
- `css/typedom/numeric/CSSMathNegate.idl` **[C15]** - CSSMathNegate: constructor, value.
- `css/typedom/numeric/CSSMathOperator.idl` **[C15]** - enums: CSSMathOperator.
- `css/typedom/numeric/CSSMathProduct.idl` **[C15]** - CSSMathProduct: constructor, values.
- `css/typedom/numeric/CSSMathSum.idl` **[C15]** - CSSMathSum: constructor, values.
- `css/typedom/numeric/CSSMathValue.idl` **[C15]** - CSSMathValue: operator.
- `css/typedom/numeric/CSSNumericArray.idl` **[C15]** - CSSNumericArray: length, <indexed operation>; collections: iterable<CSSNumericValue>;.
- `css/typedom/numeric/CSSNumericBaseType.idl` **[C15]** - enums: CSSNumericBaseType.
- `css/typedom/numeric/CSSNumericType.idl` **[C15]** - dictionaries: CSSNumericType.
- `css/typedom/StylePropertyMap.idl` **[C13]** - StylePropertyMap: set, append, delete, clear.
- `css/typedom/StylePropertyMapReadOnly.idl` **[C13]** - StylePropertyMapReadOnly: get, getAll, has, size; collections: iterable<USVString, sequence<CSSStyleValue?>>;.
- `css/typedom/transform/CSSMatrixComponent.idl` **[C16]** - CSSMatrixComponent: constructor, matrix.
- `css/typedom/transform/CSSMatrixComponentOptions.idl` **[C16]** - dictionaries: CSSMatrixComponentOptions.
- `css/typedom/transform/CSSPerspective.idl` **[C16]** - CSSPerspective: constructor, length.
- `css/typedom/transform/CSSRotate.idl` **[C16]** - CSSRotate: constructor, x, y, z, angle.
- `css/typedom/transform/CSSScale.idl` **[C16]** - CSSScale: constructor, x, y, z.
- `css/typedom/transform/CSSSkew.idl` **[C16]** - CSSSkew: constructor, ax, ay.
- `css/typedom/transform/CSSSkewX.idl` **[C16]** - CSSSkewX: constructor, ax.
- `css/typedom/transform/CSSSkewY.idl` **[C16]** - CSSSkewY: constructor, ay.
- `css/typedom/transform/CSSTransformComponent.idl` **[C16]** - CSSTransformComponent: is2D, toMatrix.
- `css/typedom/transform/CSSTransformValue.idl` **[C16]** - CSSTransformValue: constructor, length, <indexed operation>, is2D, toMatrix; collections: iterable<CSSTransformComponent>;.
- `css/typedom/transform/CSSTranslate.idl` **[C16]** - CSSTranslate: constructor, x, y, z.
### dom

- `dom/AbortAlgorithm.idl` **[I01]** - callbacks: AbortAlgorithm.
- `dom/AbortController.idl` **[I01]** - AbortController: constructor, signal, abort.
- `dom/AbortSignal.idl` **[I01]** - AbortSignal: abort, timeout, _any, aborted, reason, throwIfAborted, onabort.
- `dom/AbstractRange.idl` **[D17]** - AbstractRange: startContainer, startOffset, endContainer, endOffset, collapsed.
- `dom/AddEventListenerOptions.idl` **[I02, I08]** - dictionaries: AddEventListenerOptions.
- `dom/AriaNotifyOptions.idl` **[D17]** - dictionaries: AriaNotifyOptions; enums: AriaNotificationInterrupt, AriaNotificationPriority.
- `dom/Attr.idl` **[D17]** - Attr: namespaceURI, prefix, localName, name, value, ownerElement, specified, style.
- `dom/BeforeUnloadEvent.idl` **[B20]** - BeforeUnloadEvent: returnValue.
- `dom/BroadcastChannel.idl` **[H14]** - BroadcastChannel: constructor, name, postMessage, close, onmessage, onmessageerror.
- `dom/CaretPosition.idl` **[G09]** - CaretPosition: offsetNode, offset, getClientRect.
- `dom/CaretPositionFromPointOptions.idl` **[G09]** - dictionaries: CaretPositionFromPointOptions.
- `dom/CDATASection.idl` **[D03]** - CDATASection: no own parsed members.
- `dom/CharacterData.idl` **[D17]** - CharacterData: data, length, substringData, appendData, insertData, deleteData, replaceData; includes: CharacterData -> ChildNode, CharacterData -> NonDocumentTypeChildNode.
- `dom/CheckVisibilityOptions.idl` **[D17]** - dictionaries: CheckVisibilityOptions.
- `dom/ChildNode.idl` **[D17]** - ChildNode (mixin): before, after, replaceWith, remove.
- `dom/ClipboardEvent.idl` **[T09]** - ClipboardEvent: constructor, clipboardData; dictionaries: ClipboardEventInit.
- `dom/CommandEvent.idl` **[D16]** - CommandEvent: constructor, source, command; dictionaries: CommandEventInit.
- `dom/Comment.idl` **[D17]** - Comment: constructor.
- `dom/CompositionEvent.idl` **[T08]** - CompositionEvent: constructor, data, initCompositionEvent; dictionaries: CompositionEventInit.
- `dom/ContentVisibilityAutoStateChangeEvent.idl` **[I02, I08]** - ContentVisibilityAutoStateChangeEvent: constructor, skipped; dictionaries: ContentVisibilityAutoStateChangeEventInit.
- `dom/CreateHTMLCallback.idl` **[D17]** - callbacks: CreateHTMLCallback.
- `dom/CreateScriptCallback.idl` **[D17]** - callbacks: CreateScriptCallback.
- `dom/CreateScriptURLCallback.idl` **[D17]** - callbacks: CreateScriptURLCallback.
- `dom/CustomElementRegistry.idl` **[D07, D08, D18]** - CustomElementRegistry: constructor, define, get, getName, whenDefined, upgrade, initialize.
- `dom/CustomEvent.idl` **[I02, I08]** - CustomEvent: constructor, detail, initCustomEvent; dictionaries: CustomEventInit.
- `dom/CustomStateSet.idl` **[D07, D08, D18]** - CustomStateSet: no own parsed members; collections: setlike<[AtomString] DOMString>;.
- `dom/DataTransfer.idl` **[I07, T09]** - DataTransfer: constructor, dropEffect, effectAllowed, items, setDragImage, types, getData, setData, clearData, files.
- `dom/DataTransferItem.idl` **[I07, T09]** - DataTransferItem: kind, type, getAsString, getAsFile, webkitGetAsEntry.
- `dom/DataTransferItemList.idl` **[I07, T09]** - DataTransferItemList: length, item, add, remove, clear.
- `dom/DeviceMotionEvent.idl` **[B12]** - DeviceMotionEvent: acceleration, accelerationIncludingGravity, rotationRate, interval, requestPermission, initDeviceMotionEvent; dictionaries: Acceleration, RotationRate.
- `dom/DeviceOrientationEvent.idl` **[B12]** - DeviceOrientationEvent: alpha, beta, gamma, requestPermission, absolute, initDeviceOrientationEvent.
- `dom/DeviceOrientationOrMotionPermissionState.idl` **[B12]** - enums: DeviceOrientationOrMotionPermissionState.
- `dom/Document+CaretPositionFromPoint.idl` **[G09]** - Document (partial): caretPositionFromPoint.
- `dom/Document+CSSOMView.idl` **[G06, G02, G05]** - Document (partial): scrollingElement.
- `dom/Document+Fullscreen.idl` **[B13]** - Document (partial): fullscreenEnabled, fullscreen, exitFullscreen, onfullscreenchange, onfullscreenerror, webkitFullscreenEnabled, webkitFullscreenElement, webkitExitFullscreen, onwebkitfullscreenchange, onwebkitfullscreenerror, webkitIsFullScreen, webkitFullScreenKeyboardInputAllowed, webkitCurrentFullScreenElement, webkitCancelFullScreen.
- `dom/Document+HTML.idl` **[D17]** - Document (partial): location, domain, referrer, cookie, lastModified, readyState, title, dir, body, head, images, embeds, plugins, links, forms, scripts, getElementsByName, currentScript, open, close, write, writeln, defaultView, hasFocus, designMode, execCommand, queryCommandEnabled, queryCommandIndeterm, queryCommandState, queryCommandSupported, queryCommandValue, onreadystatechange; enums: DocumentReadyState.
- `dom/Document+HTMLObsolete.idl` **[D17]** - Document (partial): fgColor, linkColor, vlinkColor, alinkColor, bgColor, anchors, applets, clear, captureEvents, releaseEvents, all.
- `dom/Document+Immersive.idl` **[B13]** - Document (partial): immersiveEnabled, immersiveElement, exitImmersive, onimmersivechange, onimmersiveerror.
- `dom/Document+PageVisibility.idl` **[B20]** - Document (partial): hidden, visibilityState, onvisibilitychange.
- `dom/Document+PointerLock.idl` **[G07, G08]** - Document (partial): onpointerlockchange, onpointerlockerror, exitPointerLock.
- `dom/Document+Selection.idl` **[D17]** - Document (partial): getSelection.
- `dom/Document+StorageAccess.idl` **[B19]** - Document (partial): hasStorageAccess, requestStorageAccess.
- `dom/Document+Touch.idl` **[I03, I12]** - Document (partial): createTouch, createTouchList.
- `dom/Document+UndoMananger.idl` **[T05]** - Document (partial): undoManager.
- `dom/Document+ViewTransition.idl` **[A11]** - Document (partial): startViewTransition, activeViewTransition.
- `dom/Document.idl` **[D17]** - Document: constructor, parseHTMLUnsafe, fragmentDirective, implementation, URL, documentURI, compatMode, characterSet, charset, inputEncoding, contentType, doctype, documentElement, createElement, createElementNS, getElementsByTagName, getElementsByTagNameNS, getElementsByClassName, customElementRegistry, createDocumentFragment, createTextNode, createCDATASection, createComment, createProcessingInstruction, importNode, adoptNode, createAttribute, createAttributeNS, createEvent, createRange, createNodeIterator, createTreeWalker, xmlEncoding, xmlVersion, xmlStandalone, caretRangeFromPoint, getCSSCanvasContext, ariaNotify; enums: DocumentReadyState; includes: Document -> ParentNode, Document -> NonElementParentNode, Document -> DocumentOrShadowRoot, Document -> GlobalEventHandlers, Document -> DocumentAndElementEventHandlers, Document -> FontFaceSource, Document -> XPathEvaluatorBase.
- `dom/DocumentAndElementEventHandlers.idl` **[I02, I08]** - DocumentAndElementEventHandlers (mixin): onbeforecopy, onbeforecut, onbeforepaste.
- `dom/DocumentFragment.idl` **[D17]** - DocumentFragment: constructor; includes: DocumentFragment -> ParentNode, DocumentFragment -> NonElementParentNode.
- `dom/DocumentOrShadowRoot+CSSOM.idl` **[D17]** - DocumentOrShadowRoot (partial) (mixin): styleSheets, adoptedStyleSheets.
- `dom/DocumentOrShadowRoot+Fullscreen.idl` **[B13]** - DocumentOrShadowRoot (partial) (mixin): fullscreenElement.
- `dom/DocumentOrShadowRoot+PointerLock.idl` **[G07, G08]** - DocumentOrShadowRoot (partial) (mixin): pointerLockElement.
- `dom/DocumentOrShadowRoot.idl` **[D17]** - DocumentOrShadowRoot (mixin): activeElement, elementFromPoint, elementsFromPoint.
- `dom/DocumentType.idl` **[D03]** - DocumentType: name, publicId, systemId; includes: DocumentType -> ChildNode.
- `dom/DOMException.idl` **[D17]** - DOMException: constructor, code, name, message.
- `dom/DOMImplementation.idl` **[D17]** - DOMImplementation: createDocumentType, createDocument, createHTMLDocument, hasFeature.
- `dom/DOMPoint.idl` **[G07, G08]** - DOMPoint: constructor, fromPoint, x, y, z, w.
- `dom/DOMPointInit.idl` **[G07, G08]** - dictionaries: DOMPointInit.
- `dom/DOMPointReadOnly.idl` **[G07, G08]** - DOMPointReadOnly: constructor, fromPoint, x, y, z, w, matrixTransform, toJSON.
- `dom/DOMQuad.idl` **[G07, G08]** - DOMQuad: constructor, fromRect, fromQuad, p1, p2, p3, p4, getBounds, toJSON.
- `dom/DOMQuadInit.idl` **[G07, G08]** - dictionaries: DOMQuadInit.
- `dom/DOMRect.idl` **[G07, G08]** - DOMRect: constructor, fromRect, x, y, width, height.
- `dom/DOMRectInit.idl` **[G07, G08]** - dictionaries: DOMRectInit.
- `dom/DOMRectList.idl` **[G07, G08]** - DOMRectList: length, item.
- `dom/DOMRectReadOnly.idl` **[G07, G08]** - DOMRectReadOnly: constructor, fromRect, x, y, width, height, top, right, bottom, left, toJSON.
- `dom/DOMStringList.idl` **[D17]** - DOMStringList: length, item, contains.
- `dom/DOMStringMap.idl` **[D17]** - DOMStringMap: <indexed operation>.
- `dom/DragEvent.idl` **[I07, T09]** - DragEvent: constructor, dataTransfer; dictionaries: DragEventInit.
- `dom/Element+CSSOMView.idl` **[G06, G02, G05]** - Element (partial): getClientRects, getBoundingClientRect, checkVisibility, scrollIntoView, scroll, scrollTo, scrollBy, scrollTop, scrollLeft, scrollWidth, scrollHeight, clientTop, clientLeft, clientWidth, clientHeight, currentCSSZoom, scrollIntoViewIfNeeded.
- `dom/Element+DOMParsing.idl` **[D17]** - Element (partial): outerHTML, insertAdjacentHTML.
- `dom/Element+Fullscreen.idl` **[B13]** - Element (partial): requestFullscreen, onfullscreenchange, onfullscreenerror, webkitRequestFullScreen, webkitRequestFullscreen, onwebkitfullscreenchange, onwebkitfullscreenerror.
- `dom/Element+PointerEvents.idl` **[G07, G08]** - Element (partial): setPointerCapture, releasePointerCapture, hasPointerCapture.
- `dom/Element+PointerLock.idl` **[G07, G08]** - Element (partial): requestPointerLock.
- `dom/Element+Typedom.idl` **[C13]** - Element (partial): computedStyleMap.
- `dom/Element.idl` **[D17]** - Element: namespaceURI, prefix, localName, tagName, id, className, classList, slot, part, customElementRegistry, hasAttributes, attributes, getAttributeNames, getAttribute, getAttributeNS, setAttribute, setAttributeNS, removeAttribute, removeAttributeNS, toggleAttribute, hasAttribute, hasAttributeNS, getAttributeNode, getAttributeNodeNS, setAttributeNode, setAttributeNodeNS, removeAttributeNode, attachShadow, shadowRoot, openOrClosedShadowRoot, closest, matches, webkitMatchesSelector, getElementsByTagName, getElementsByTagNameNS, getElementsByClassName, insertAdjacentElement, insertAdjacentText, onfocusin, onfocusout, ongesturechange, ongestureend, ongesturestart, onbeforeload, onwebkitneedkey, onwebkitpresentationmodechanged, onwebkitcurrentplaybacktargetiswirelesschanged, onwebkitplaybacktargetavailabilitychanged, setUserInfo, ariaNotify; includes: Element -> AccessibilityRole, Element -> AriaAttributes, Element -> Animatable, Element -> ChildNode, Element -> NonDocumentTypeChildNode, Element -> ParentNode, Element -> Slotable, Element -> InnerHTML.
- `dom/ElementContentEditable.idl` **[D17]** - ElementContentEditable (mixin): contentEditable, enterKeyHint, isContentEditable, inputMode.
- `dom/ElementCreationOptions.idl` **[D17]** - dictionaries: ElementCreationOptions.
- `dom/ElementInternals.idl` **[D08, I11]** - ElementInternals: shadowRoot, setFormValue, form, setValidity, willValidate, validity, validationMessage, checkValidity, reportValidity, labels, states, role, ariaActiveDescendantElement, ariaAtomic, ariaAutoComplete, ariaBrailleLabel, ariaBrailleRoleDescription, ariaBusy, ariaChecked, ariaColCount, ariaColIndex, ariaColSpan, ariaControlsElements, ariaActionsElements, ariaCurrent, ariaDescribedByElements, ariaDetailsElements, ariaDisabled, ariaErrorMessageElements, ariaExpanded, ariaFlowToElements, ariaHasPopup, ariaHidden, ariaInvalid, ariaKeyShortcuts, ariaLabel, ariaLabelledByElements, ariaLevel, ariaLive, ariaModal, ariaMultiLine, ariaMultiSelectable, ariaOrientation, ariaOwnsElements, ariaPlaceholder, ariaPosInSet, ariaPressed, ariaReadOnly, ariaRelevant, ariaRequired, ariaRoleDescription, ariaRowCount, ariaRowIndex, ariaRowSpan, ariaSelected, ariaSetSize, ariaSort, ariaValueMax, ariaValueMin, ariaValueNow, ariaValueText.
- `dom/ErrorEvent.idl` **[H10, I08]** - ErrorEvent: constructor, message, filename, lineno, colno, error; dictionaries: ErrorEventInit.
- `dom/Event.idl` **[I02, I08]** - Event: constructor, type, target, currentTarget, composedPath, eventPhase, stopPropagation, cancelBubble, stopImmediatePropagation, bubbles, cancelable, preventDefault, defaultPrevented, composed, isTrusted, timeStamp, initEvent, srcElement, returnValue.
- `dom/EventInit.idl` **[I02, I08]** - dictionaries: EventInit.
- `dom/EventListener.idl` **[I02, I08]** - EventListener: handleEvent.
- `dom/EventListenerOptions.idl` **[I02, I08]** - dictionaries: EventListenerOptions.
- `dom/EventModifierInit.idl` **[I02, I08]** - dictionaries: EventModifierInit.
- `dom/EventTarget.idl` **[I02, I08]** - EventTarget: constructor, addEventListener, removeEventListener, dispatchEvent.
- `dom/FocusEvent.idl` **[I02, I08]** - FocusEvent: constructor, relatedTarget; dictionaries: FocusEventInit.
- `dom/FocusOptions.idl` **[D17]** - dictionaries: FocusOptions.
- `dom/FormDataEvent.idl` **[D13]** - FormDataEvent: constructor, formData; dictionaries: FormDataEventInit.
- `dom/FullscreenOptions.idl` **[B13]** - dictionaries: FullscreenOptions; enums: FullscreenKeyboardLock, FullscreenNavigationUI.
- `dom/GetHTMLOptions.idl` **[D17]** - dictionaries: GetHTMLOptions.
- `dom/GlobalEventHandlers+PointerEvents.idl` **[G07, G08]** - GlobalEventHandlers (partial) (mixin): ongotpointercapture, onlostpointercapture, onpointerdown, onpointermove, onpointerup, onpointercancel, onpointerover, onpointerout, onpointerenter, onpointerleave.
- `dom/GlobalEventHandlers+Selection.idl` **[I02, I08]** - GlobalEventHandlers (partial) (mixin): onselectstart, onselectionchange.
- `dom/GlobalEventHandlers.idl` **[I02, I08]** - GlobalEventHandlers (mixin): onabort, onauxclick, onbeforeinput, onbeforematch, onbeforetoggle, onblur, oncancel, oncanplay, oncanplaythrough, onchange, onclick, onclose, oncommand, oncontentvisibilityautostatechange, oncontextmenu, oncopy, oncuechange, oncut, ondblclick, ondrag, ondragend, ondragenter, ondragleave, ondragover, ondragstart, ondrop, ondurationchange, onemptied, onended, onerror, onfocus, onformdata, oninput, oninvalid, onkeydown, onkeypress, onkeyup, onload, onloadeddata, onloadedmetadata, onloadstart, onmousedown, onmouseenter, onmouseleave, onmousemove, onmouseout, onmouseover, onmouseup, onpaste, onpause, onplay, onplaying, onprogress, onratechange, onreset, onresize, onscroll, onscrollend, onsecuritypolicyviolation, onseeked, onseeking, onselect, onslotchange, onstalled, onsubmit, onsuspend, ontimeupdate, ontoggle, onvolumechange, onwaiting, onwebkitanimationend, onwebkitanimationiteration, onwebkitanimationstart, onwebkittransitionend, onwheel, onmousewheel, ontouchcancel, ontouchend, ontouchmove, ontouchstart, ontouchforcechange, onwebkitmouseforcechanged, onwebkitmouseforcedown, onwebkitmouseforcewillbegin, onwebkitmouseforceup.
- `dom/HashChangeEvent.idl` **[B20]** - HashChangeEvent: constructor, oldURL, newURL; dictionaries: HashChangeEventInit.
- `dom/IdleDeadline.idl` **[H01]** - IdleDeadline: timeRemaining, didTimeout.
- `dom/IdleRequestCallback.idl` **[H01]** - callbacks: IdleRequestCallback.
- `dom/IdleRequestOptions.idl` **[H01]** - dictionaries: IdleRequestOptions.
- `dom/ImportNodeOptions.idl` **[D17]** - dictionaries: ImportNodeOptions.
- `dom/InnerHTML.idl` **[D17]** - InnerHTML (mixin): setHTMLUnsafe, getHTML, innerHTML.
- `dom/InputEvent.idl` **[T04]** - InputEvent: constructor, inputType, data, isComposing, dataTransfer, getTargetRanges; dictionaries: InputEventInit.
- `dom/KeyboardEvent.idl` **[I02, I08]** - KeyboardEvent: constructor, key, code, location, ctrlKey, shiftKey, altKey, metaKey, repeat, isComposing, getModifierState, keyIdentifier, charCode, keyCode, initKeyboardEvent; dictionaries: KeyboardEventInit.
- `dom/MapperCallback.idl` **[I13]** - callbacks: MapperCallback.
- `dom/MessageChannel.idl` **[H14]** - MessageChannel: constructor, port1, port2.
- `dom/MessageEvent.idl` **[H14]** - MessageEvent: constructor, origin, lastEventId, source, data, ports, initMessageEvent; dictionaries: MessageEventInit.
- `dom/MessagePort.idl` **[H14]** - MessagePort: postMessage, start, close, onmessage, onmessageerror.
- `dom/MouseEvent.idl` **[I02, I08]** - MouseEvent: constructor, screenX, screenY, clientX, clientY, ctrlKey, shiftKey, altKey, metaKey, button, buttons, relatedTarget, getModifierState, movementX, movementY, webkitForce, initMouseEvent, offsetX, offsetY, x, y, fromElement, toElement.
- `dom/MouseEventInit.idl` **[I02, I08]** - dictionaries: MouseEventInit.
- `dom/MutationCallback.idl` **[D02]** - callbacks: MutationCallback.
- `dom/MutationEvent.idl` **[D02]** - MutationEvent: relatedNode, prevValue, newValue, attrName, attrChange, initMutationEvent.
- `dom/MutationObserver.idl` **[D02]** - MutationObserver: constructor, observe, disconnect, takeRecords; dictionaries: MutationObserverInit.
- `dom/MutationRecord.idl` **[D02]** - MutationRecord: type, target, addedNodes, removedNodes, previousSibling, nextSibling, attributeName, attributeNamespace, oldValue.
- `dom/NamedNodeMap.idl` **[D17]** - NamedNodeMap: length, item, getNamedItem, getNamedItemNS, setNamedItem, setNamedItemNS, removeNamedItem, removeNamedItemNS.
- `dom/NavigatorMaxTouchPoints.idl` **[G07, G08]** - NavigatorMaxTouchPoints (mixin): maxTouchPoints.
- `dom/Node.idl` **[D17]** - Node: nodeType, nodeName, baseURI, isConnected, ownerDocument, getRootNode, parentNode, parentElement, hasChildNodes, childNodes, firstChild, lastChild, previousSibling, nextSibling, nodeValue, textContent, normalize, cloneNode, isEqualNode, isSameNode, compareDocumentPosition, contains, lookupPrefix, lookupNamespaceURI, isDefaultNamespace, insertBefore, appendChild, replaceChild, removeChild; dictionaries: GetRootNodeOptions.
- `dom/NodeFilter.idl` **[D01]** - callbacks: NodeFilter.
- `dom/NodeIterator.idl` **[D01]** - NodeIterator: root, referenceNode, pointerBeforeReferenceNode, whatToShow, filter, nextNode, previousNode, detach.
- `dom/NodeList.idl` **[D17]** - NodeList: item, length; collections: iterable<Node>;.
- `dom/NonDocumentTypeChildNode.idl` **[D03]** - NonDocumentTypeChildNode (mixin): previousElementSibling, nextElementSibling.
- `dom/NonElementParentNode.idl` **[D17]** - NonElementParentNode (mixin): getElementById.
- `dom/Observable.idl` **[I13]** - Observable: constructor, subscribe, map, filter, take, drop, inspect, first, forEach, last, find, every, some, reduce.
- `dom/ObservableInspector.idl` **[I13]** - dictionaries: ObservableInspector.
- `dom/ObservableInspectorAbortCallback.idl` **[I01]** - callbacks: ObservableInspectorAbortCallback.
- `dom/PageRevealEvent.idl` **[B20]** - PageRevealEvent: constructor, viewTransition; dictionaries: PageRevealEventInit.
- `dom/PageSwapEvent.idl` **[B20]** - PageSwapEvent: constructor, activation, viewTransition; dictionaries: PageSwapEventInit.
- `dom/PageTransitionEvent.idl` **[B20]** - PageTransitionEvent: constructor, persisted; dictionaries: PageTransitionEventInit.
- `dom/ParentNode.idl` **[D17]** - ParentNode (mixin): children, firstElementChild, lastElementChild, childElementCount, prepend, append, replaceChildren, moveBefore, querySelector, querySelectorAll.
- `dom/PointerEvent.idl` **[G07, G08]** - PointerEvent: constructor, pointerId, width, height, pressure, tangentialPressure, tiltX, tiltY, twist, altitudeAngle, azimuthAngle, pointerType, isPrimary, getCoalescedEvents, getPredictedEvents; dictionaries: PointerEventInit.
- `dom/PointerLockOptions.idl` **[G07, G08]** - dictionaries: PointerLockOptions.
- `dom/PopStateEvent.idl` **[B20]** - PopStateEvent: constructor, state, hasUAVisualTransition; dictionaries: PopStateEventInit.
- `dom/PredicateCallback.idl` **[I13]** - callbacks: PredicateCallback.
- `dom/ProcessingInstruction.idl` **[D03]** - ProcessingInstruction: target, sheet.
- `dom/ProgressEvent.idl` **[H10, I08]** - ProgressEvent: constructor, lengthComputable, loaded, total; dictionaries: ProgressEventInit.
- `dom/PromiseRejectionEvent.idl` **[H10, I08]** - PromiseRejectionEvent: constructor, promise, reason; dictionaries: PromiseRejectionEventInit.
- `dom/Range+CSSOMView.idl` **[G06, G02, G05]** - Range (partial): getClientRects, getBoundingClientRect.
- `dom/Range+DOMParsing.idl` **[D17]** - Range (partial): createContextualFragment.
- `dom/Range.idl` **[D17]** - Range: constructor, commonAncestorContainer, setStart, setEnd, setStartBefore, setStartAfter, setEndBefore, setEndAfter, collapse, selectNode, selectNodeContents, compareBoundaryPoints, deleteContents, extractContents, cloneContents, insertNode, surroundContents, cloneRange, detach, isPointInRange, comparePoint, intersectsNode, expand, compareNode.
- `dom/ReducerCallback.idl` **[I13]** - callbacks: ReducerCallback.
- `dom/RequestAnimationFrameCallback.idl` **[D17]** - callbacks: RequestAnimationFrameCallback.
- `dom/SecurityPolicyViolationEvent.idl` **[B21]** - SecurityPolicyViolationEvent: constructor, documentURI, referrer, blockedURI, effectiveDirective, violatedDirective, originalPolicy, sourceFile, sample, disposition, statusCode, lineno, lineNumber, colno, columnNumber; dictionaries: SecurityPolicyViolationEventInit.
- `dom/SecurityPolicyViolationEventDisposition.idl` **[B21]** - enums: SecurityPolicyViolationEventDisposition.
- `dom/ShadowRoot.idl` **[D17]** - ShadowRoot: mode, delegatesFocus, slotAssignment, clonable, serializable, host, customElementRegistry, referenceTarget, onslotchange; includes: ShadowRoot -> DocumentOrShadowRoot, ShadowRoot -> InnerHTML.
- `dom/ShadowRootInit.idl` **[D17]** - dictionaries: ShadowRootInit.
- `dom/ShadowRootMode.idl` **[D17]** - enums: ShadowRootMode.
- `dom/Slotable.idl` **[D17]** - Slotable (mixin): assignedSlot.
- `dom/SlotAssignmentMode.idl` **[D17]** - enums: SlotAssignmentMode.
- `dom/StartViewTransitionOptions.idl` **[A11]** - dictionaries: StartViewTransitionOptions.
- `dom/StaticRange.idl` **[D17]** - StaticRange: constructor; dictionaries: StaticRangeInit.
- `dom/StringCallback.idl` **[D17]** - callbacks: StringCallback.
- `dom/SubscribeOptions.idl` **[D17]** - dictionaries: SubscribeOptions.
- `dom/Subscriber.idl` **[I13]** - Subscriber: next, error, complete, addTeardown, active, signal.
- `dom/SubscriberCallback.idl` **[I13]** - callbacks: SubscriberCallback.
- `dom/SubscriptionObserver.idl` **[I13]** - dictionaries: SubscriptionObserver.
- `dom/SubscriptionObserverCallback.idl` **[I13]** - callbacks: SubscriptionObserverCallback.
- `dom/Text.idl` **[D17]** - Text: constructor, splitText, wholeText; includes: Text -> Slotable.
- `dom/TextDecoder.idl` **[H12]** - TextDecoder: constructor, encoding, fatal, ignoreBOM, decode; dictionaries: TextDecodeOptions, TextDecoderOptions.
- `dom/TextDecoderStream.idl` **[H12]** - TextDecoderStream: constructor, encoding, fatal, ignoreBOM, readable, writable.
- `dom/TextDecoderStreamDecoder.idl` **[H12]** - TextDecoderStreamDecoder: constructor, encoding, decode, flush.
- `dom/TextEncoder.idl` **[H12]** - TextEncoder: constructor, encoding, encode, encodeInto; dictionaries: TextEncoderEncodeIntoResult.
- `dom/TextEncoderStream.idl` **[H12]** - TextEncoderStream: constructor, encoding, readable, writable.
- `dom/TextEncoderStreamEncoder.idl` **[H12]** - TextEncoderStreamEncoder: constructor, encode, flush.
- `dom/TextEvent.idl` **[T08]** - TextEvent: data, initTextEvent.
- `dom/ToggleEvent.idl` **[D16]** - ToggleEvent: constructor, oldState, newState, source; dictionaries: ToggleEventInit.
- `dom/Touch.idl` **[I03, I12]** - Touch: clientX, clientY, screenX, screenY, pageX, pageY, target, identifier, webkitRadiusX, webkitRadiusY, webkitRotationAngle, webkitForce.
- `dom/TouchEvent.idl` **[I03, I12]** - TouchEvent: constructor, touches, targetTouches, changedTouches, ctrlKey, shiftKey, altKey, metaKey; dictionaries: TouchEventInit.
- `dom/TouchList.idl` **[I03, I12]** - TouchList: length, item.
- `dom/TreeWalker.idl` **[D01]** - TreeWalker: root, whatToShow, filter, currentNode, parentNode, firstChild, lastChild, previousSibling, nextSibling, previousNode, nextNode.
- `dom/TrustedHTML.idl` **[B21]** - TrustedHTML: toJSON.
- `dom/TrustedScript.idl` **[B21]** - TrustedScript: toJSON.
- `dom/TrustedScriptURL.idl` **[B21]** - TrustedScriptURL: toJSON.
- `dom/TrustedTypePolicy.idl` **[B21]** - TrustedTypePolicy: name, createHTML, createScript, createScriptURL.
- `dom/TrustedTypePolicyFactory.idl` **[B21]** - TrustedTypePolicyFactory: createPolicy, isHTML, isScript, isScriptURL, emptyHTML, emptyScript, getAttributeType, getPropertyType, defaultPolicy.
- `dom/TrustedTypePolicyOptions.idl` **[B21]** - dictionaries: TrustedTypePolicyOptions.
- `dom/UIEvent.idl` **[I02, I08]** - UIEvent: constructor, view, detail, initUIEvent, layerX, layerY, pageX, pageY, which.
- `dom/UIEventInit.idl` **[I02, I08]** - dictionaries: UIEventInit.
- `dom/ValidityStateFlags.idl` **[D17]** - dictionaries: ValidityStateFlags.
- `dom/ViewTransition+Types.idl` **[A11]** - ViewTransition (partial): types.
- `dom/ViewTransition.idl` **[A11]** - ViewTransition: updateCallbackDone, ready, finished, skipTransition.
- `dom/ViewTransitionTypeSet.idl` **[A11]** - ViewTransitionTypeSet: no own parsed members; collections: setlike<[AtomString] DOMString>;.
- `dom/ViewTransitionUpdateCallback.idl` **[A11]** - callbacks: ViewTransitionUpdateCallback.
- `dom/VisibilityState.idl` **[D17]** - enums: VisibilityState.
- `dom/VisitorCallback.idl` **[I13]** - callbacks: VisitorCallback.
- `dom/WheelEvent.idl` **[I02, I08]** - WheelEvent: constructor, deltaX, deltaY, deltaZ, deltaMode, wheelDeltaX, wheelDeltaY, wheelDelta, webkitDirectionInvertedFromDevice; dictionaries: WheelEventInit.
- `dom/WindowOrWorkerGlobalScope+TrustedTypes.idl` **[B21]** - WindowOrWorkerGlobalScope (partial) (mixin): trustedTypes.
- `dom/XMLDocument.idl` **[D03]** - XMLDocument: no own parsed members.
### fileapi

- `fileapi/Blob.idl` **[H11]** - Blob: constructor, size, type, slice, stream, text, arrayBuffer, bytes.
- `fileapi/BlobCallback.idl` **[H11]** - callbacks: BlobCallback.
- `fileapi/BlobPropertyBag.idl` **[H11]** - dictionaries: BlobPropertyBag.
- `fileapi/EndingType.idl` **[H11]** - enums: EndingType.
- `fileapi/File.idl` **[H11]** - File: constructor, name, lastModified, webkitRelativePath; dictionaries: FilePropertyBag.
- `fileapi/FileList.idl` **[H11]** - FileList: length, item.
- `fileapi/FileReader.idl` **[H11]** - FileReader: constructor, readyState, readAsArrayBuffer, readAsBinaryString, readAsText, readAsDataURL, abort, result, error, onloadstart, onprogress, onload, onabort, onerror, onloadend.
- `fileapi/FileReaderSync.idl` **[H11]** - FileReaderSync: constructor, readAsArrayBuffer, readAsBinaryString, readAsText, readAsDataURL.
### html

- `html/canvas/ANGLEInstancedArrays.idl` **[R10]** - ANGLEInstancedArrays: drawArraysInstancedANGLE, drawElementsInstancedANGLE, vertexAttribDivisorANGLE.
- `html/canvas/CanvasCompositing.idl` **[R01, R02, R03, R04, R05, R06, R07, R08]** - CanvasCompositing (mixin): globalAlpha, globalCompositeOperation.
- `html/canvas/CanvasDirection.idl` **[R01, R02, R03, R04, R05, R06, R07, R08]** - enums: CanvasDirection.
- `html/canvas/CanvasDrawImage.idl` **[R01, R02, R03, R04, R05, R06, R07, R08]** - CanvasDrawImage (mixin): drawImage.
- `html/canvas/CanvasDrawPath.idl` **[R01, R02, R03, R04, R05, R06, R07, R08]** - CanvasDrawPath (mixin): beginPath, fill, stroke, clip, isPointInPath, isPointInStroke.
- `html/canvas/CanvasFillRule.idl` **[R01, R02, R03, R04, R05, R06, R07, R08]** - enums: CanvasFillRule.
- `html/canvas/CanvasFillStrokeStyles.idl` **[R01, R02, R03, R04, R05, R06, R07, R08]** - CanvasFillStrokeStyles (mixin): strokeStyle, fillStyle, createLinearGradient, createRadialGradient, createConicGradient, createPattern.
- `html/canvas/CanvasFilters.idl` **[R01, R02, R03, R04, R05, R06, R07, R08]** - CanvasFilters (mixin): filter.
- `html/canvas/CanvasGradient.idl` **[R01, R02, R03, R04, R05, R06, R07, R08]** - CanvasGradient: addColorStop.
- `html/canvas/CanvasImageData.idl` **[R01, R02, R03, R04, R05, R06, R07, R08]** - CanvasImageData (mixin): createImageData, getImageData, putImageData.
- `html/canvas/CanvasImageSmoothing.idl` **[R01, R02, R03, R04, R05, R06, R07, R08]** - CanvasImageSmoothing (mixin): imageSmoothingEnabled, imageSmoothingQuality.
- `html/canvas/CanvasLayers.idl` **[R01, R02, R03, R04, R05, R06, R07, R08]** - CanvasLayers (mixin): beginLayer, endLayer.
- `html/canvas/CanvasLineCap.idl` **[R01, R02, R03, R04, R05, R06, R07, R08]** - enums: CanvasLineCap.
- `html/canvas/CanvasLineJoin.idl` **[R01, R02, R03, R04, R05, R06, R07, R08]** - enums: CanvasLineJoin.
- `html/canvas/CanvasPath.idl` **[R01, R02, R03, R04, R05, R06, R07, R08]** - CanvasPath (mixin): closePath, moveTo, lineTo, quadraticCurveTo, bezierCurveTo, arcTo, rect, roundRect, arc, ellipse.
- `html/canvas/CanvasPathDrawingStyles.idl` **[R01, R02, R03, R04, R05, R06, R07, R08]** - CanvasPathDrawingStyles (mixin): lineWidth, lineCap, lineJoin, miterLimit, setLineDash, getLineDash, lineDashOffset.
- `html/canvas/CanvasPattern.idl` **[R01, R02, R03, R04, R05, R06, R07, R08]** - CanvasPattern: setTransform.
- `html/canvas/CanvasRect.idl` **[R01, R02, R03, R04, R05, R06, R07, R08]** - CanvasRect (mixin): clearRect, fillRect, strokeRect.
- `html/canvas/CanvasRenderingContext2D.idl` **[R01, R02, R03, R04, R05, R06, R07, R08]** - CanvasRenderingContext2D: canvas, getContextAttributes, setStrokeColor, setFillColor, setLineWidth, setLineCap, setLineJoin, setMiterLimit, webkitLineDash, webkitLineDashOffset, setShadow, clearShadow, getEffectiveRenderingModeForTesting; enums: RenderingMode; includes: CanvasRenderingContext2D -> CanvasState, CanvasRenderingContext2D -> CanvasLayers, CanvasRenderingContext2D -> CanvasTransform, CanvasRenderingContext2D -> CanvasCompositing, CanvasRenderingContext2D -> CanvasImageSmoothing, CanvasRenderingContext2D -> CanvasFillStrokeStyles, CanvasRenderingContext2D -> CanvasShadowStyles, CanvasRenderingContext2D -> CanvasFilters, CanvasRenderingContext2D -> CanvasRect, CanvasRenderingContext2D -> CanvasDrawPath, CanvasRenderingContext2D -> CanvasUserInterface, CanvasRenderingContext2D -> CanvasText, CanvasRenderingContext2D -> CanvasDrawImage, CanvasRenderingContext2D -> CanvasImageData, CanvasRenderingContext2D -> CanvasPathDrawingStyles, CanvasRenderingContext2D -> CanvasTextDrawingStyles, CanvasRenderingContext2D -> CanvasPath.
- `html/canvas/CanvasRenderingContext2DSettings.idl` **[R01, R02, R03, R04, R05, R06, R07, R08]** - dictionaries: CanvasRenderingContext2DSettings; enums: CanvasColorType, RenderingMode.
- `html/canvas/CanvasShadowStyles.idl` **[R01, R02, R03, R04, R05, R06, R07, R08]** - CanvasShadowStyles (mixin): shadowOffsetX, shadowOffsetY, shadowBlur, shadowColor.
- `html/canvas/CanvasState.idl` **[R01, R02, R03, R04, R05, R06, R07, R08]** - CanvasState (mixin): save, restore, reset.
- `html/canvas/CanvasText.idl` **[R01, R02, R03, R04, R05, R06, R07, R08]** - CanvasText (mixin): fillText, strokeText, measureText.
- `html/canvas/CanvasTextAlign.idl` **[R01, R02, R03, R04, R05, R06, R07, R08]** - enums: CanvasTextAlign.
- `html/canvas/CanvasTextBaseline.idl` **[R01, R02, R03, R04, R05, R06, R07, R08]** - enums: CanvasTextBaseline.
- `html/canvas/CanvasTextDrawingStyles.idl` **[R01, R02, R03, R04, R05, R06, R07, R08]** - CanvasTextDrawingStyles (mixin): font, textAlign, textBaseline, direction, letterSpacing, wordSpacing.
- `html/canvas/CanvasTransform.idl` **[R01, R02, R03, R04, R05, R06, R07, R08]** - CanvasTransform (mixin): scale, rotate, translate, transform, getTransform, setTransform, resetTransform.
- `html/canvas/CanvasUserInterface.idl` **[R01, R02, R03, R04, R05, R06, R07, R08]** - CanvasUserInterface (mixin): drawFocusIfNeeded.
- `html/canvas/EXTBlendMinMax.idl` **[R10]** - EXTBlendMinMax: no own parsed members.
- `html/canvas/EXTClipControl.idl` **[R10]** - EXTClipControl: clipControlEXT.
- `html/canvas/EXTColorBufferFloat.idl` **[R10]** - EXTColorBufferFloat: no own parsed members.
- `html/canvas/EXTColorBufferHalfFloat.idl` **[R10]** - EXTColorBufferHalfFloat: no own parsed members.
- `html/canvas/EXTConservativeDepth.idl` **[R10]** - EXTConservativeDepth: no own parsed members.
- `html/canvas/EXTDepthClamp.idl` **[R10]** - EXTDepthClamp: no own parsed members.
- `html/canvas/EXTDisjointTimerQuery.idl` **[R10]** - EXTDisjointTimerQuery: createQueryEXT, deleteQueryEXT, isQueryEXT, beginQueryEXT, endQueryEXT, queryCounterEXT, getQueryEXT, getQueryObjectEXT.
- `html/canvas/EXTDisjointTimerQueryWebGL2.idl` **[R10]** - EXTDisjointTimerQueryWebGL2: queryCounterEXT.
- `html/canvas/EXTFloatBlend.idl` **[R10]** - EXTFloatBlend: no own parsed members.
- `html/canvas/EXTFragDepth.idl` **[R10]** - EXTFragDepth: no own parsed members.
- `html/canvas/EXTPolygonOffsetClamp.idl` **[R10]** - EXTPolygonOffsetClamp: polygonOffsetClampEXT.
- `html/canvas/EXTRenderSnorm.idl` **[R10]** - EXTRenderSnorm: no own parsed members.
- `html/canvas/EXTShaderTextureLOD.idl` **[R10]** - EXTShaderTextureLOD: no own parsed members.
- `html/canvas/EXTsRGB.idl` **[R10]** - EXTsRGB: no own parsed members.
- `html/canvas/EXTTextureCompressionBPTC.idl` **[R10]** - EXTTextureCompressionBPTC: no own parsed members.
- `html/canvas/EXTTextureCompressionRGTC.idl` **[R10]** - EXTTextureCompressionRGTC: no own parsed members.
- `html/canvas/EXTTextureFilterAnisotropic.idl` **[R10]** - EXTTextureFilterAnisotropic: no own parsed members.
- `html/canvas/EXTTextureMirrorClampToEdge.idl` **[R10]** - EXTTextureMirrorClampToEdge: no own parsed members.
- `html/canvas/EXTTextureNorm16.idl` **[R10]** - EXTTextureNorm16: no own parsed members.
- `html/canvas/GPUCanvasContext.idl` **[R11]** - GPUCanvasContext: canvas, configure, unconfigure, getConfiguration, getCurrentTexture.
- `html/canvas/ImageBitmapRenderingContext.idl` **[R01, R02, R03, R04, R05, R06, R07, R08]** - ImageBitmapRenderingContext: canvas, transferFromImageBitmap.
- `html/canvas/ImageBitmapRenderingContextSettings.idl` **[R01, R02, R03, R04, R05, R06, R07, R08]** - dictionaries: ImageBitmapRenderingContextSettings.
- `html/canvas/ImageSmoothingQuality.idl` **[R09]** - enums: ImageSmoothingQuality.
- `html/canvas/KHRParallelShaderCompile.idl` **[R10]** - KHRParallelShaderCompile: no own parsed members.
- `html/canvas/NVShaderNoperspectiveInterpolation.idl` **[R10]** - NVShaderNoperspectiveInterpolation: no own parsed members.
- `html/canvas/OESDrawBuffersIndexed.idl` **[R10]** - OESDrawBuffersIndexed: enableiOES, disableiOES, blendEquationiOES, blendEquationSeparateiOES, blendFunciOES, blendFuncSeparateiOES, colorMaskiOES.
- `html/canvas/OESElementIndexUint.idl` **[R10]** - OESElementIndexUint: no own parsed members.
- `html/canvas/OESFBORenderMipmap.idl` **[R10]** - OESFBORenderMipmap: no own parsed members.
- `html/canvas/OESSampleVariables.idl` **[R10]** - OESSampleVariables: no own parsed members.
- `html/canvas/OESShaderMultisampleInterpolation.idl` **[R10]** - OESShaderMultisampleInterpolation: no own parsed members.
- `html/canvas/OESStandardDerivatives.idl` **[R10]** - OESStandardDerivatives: no own parsed members.
- `html/canvas/OESTextureFloat.idl` **[R10]** - OESTextureFloat: no own parsed members.
- `html/canvas/OESTextureFloatLinear.idl` **[R10]** - OESTextureFloatLinear: no own parsed members.
- `html/canvas/OESTextureHalfFloat.idl` **[R10]** - OESTextureHalfFloat: no own parsed members.
- `html/canvas/OESTextureHalfFloatLinear.idl` **[R10]** - OESTextureHalfFloatLinear: no own parsed members.
- `html/canvas/OESVertexArrayObject.idl` **[R10]** - OESVertexArrayObject: createVertexArrayOES, deleteVertexArrayOES, isVertexArrayOES, bindVertexArrayOES.
- `html/canvas/OffscreenCanvasRenderingContext2D.idl` **[R01, R02, R03, R04, R05, R06, R07, R08]** - OffscreenCanvasRenderingContext2D: canvas; includes: OffscreenCanvasRenderingContext2D -> CanvasState, OffscreenCanvasRenderingContext2D -> CanvasLayers, OffscreenCanvasRenderingContext2D -> CanvasTransform, OffscreenCanvasRenderingContext2D -> CanvasCompositing, OffscreenCanvasRenderingContext2D -> CanvasImageSmoothing, OffscreenCanvasRenderingContext2D -> CanvasFillStrokeStyles, OffscreenCanvasRenderingContext2D -> CanvasShadowStyles, OffscreenCanvasRenderingContext2D -> CanvasFilters, OffscreenCanvasRenderingContext2D -> CanvasRect, OffscreenCanvasRenderingContext2D -> CanvasDrawPath, OffscreenCanvasRenderingContext2D -> CanvasText, OffscreenCanvasRenderingContext2D -> CanvasDrawImage, OffscreenCanvasRenderingContext2D -> CanvasImageData, OffscreenCanvasRenderingContext2D -> CanvasPathDrawingStyles, OffscreenCanvasRenderingContext2D -> CanvasTextDrawingStyles, OffscreenCanvasRenderingContext2D -> CanvasPath.
- `html/canvas/PaintRenderingContext2D.idl` **[D11]** - PaintRenderingContext2D: no own parsed members; includes: PaintRenderingContext2D -> CanvasState, PaintRenderingContext2D -> CanvasTransform, PaintRenderingContext2D -> CanvasCompositing, PaintRenderingContext2D -> CanvasImageSmoothing, PaintRenderingContext2D -> CanvasFillStrokeStyles, PaintRenderingContext2D -> CanvasShadowStyles, PaintRenderingContext2D -> CanvasRect, PaintRenderingContext2D -> CanvasDrawPath, PaintRenderingContext2D -> CanvasDrawImage, PaintRenderingContext2D -> CanvasPathDrawingStyles, PaintRenderingContext2D -> CanvasPath.
- `html/canvas/Path2D.idl` **[R01, R02, R03, R04, R05, R06, R07, R08]** - Path2D: constructor, addPath; includes: Path2D -> CanvasPath.
- `html/canvas/PredefinedColorSpace.idl` **[D11]** - enums: PredefinedColorSpace.
- `html/canvas/WebGL2RenderingContext.idl` **[R10]** - WebGL2RenderingContext: bufferData, bufferSubData, copyBufferSubData, getBufferSubData, blitFramebuffer, framebufferTextureLayer, invalidateFramebuffer, invalidateSubFramebuffer, readBuffer, getInternalformatParameter, renderbufferStorageMultisample, texStorage2D, texStorage3D, texImage3D, texSubImage3D, copyTexSubImage3D, compressedTexImage3D, compressedTexSubImage3D, getFragDataLocation, uniform1ui, uniform2ui, uniform3ui, uniform4ui, uniform1uiv, uniform2uiv, uniform3uiv, uniform4uiv, uniformMatrix2x3fv, uniformMatrix3x2fv, uniformMatrix2x4fv, uniformMatrix4x2fv, uniformMatrix3x4fv, uniformMatrix4x3fv, vertexAttribI4i, vertexAttribI4iv, vertexAttribI4ui, vertexAttribI4uiv, vertexAttribIPointer, vertexAttribDivisor, drawArraysInstanced, drawElementsInstanced, drawRangeElements, drawBuffers, clearBufferiv, clearBufferuiv, clearBufferfv, clearBufferfi, createQuery, deleteQuery, isQuery, beginQuery, endQuery, getQuery, getQueryParameter, createSampler, deleteSampler, isSampler, bindSampler, samplerParameteri, samplerParameterf, getSamplerParameter, fenceSync, isSync, deleteSync, clientWaitSync, waitSync, getSyncParameter, createTransformFeedback, deleteTransformFeedback, isTransformFeedback, bindTransformFeedback, beginTransformFeedback, endTransformFeedback, transformFeedbackVaryings, getTransformFeedbackVarying, pauseTransformFeedback, resumeTransformFeedback, bindBufferBase, bindBufferRange, getIndexedParameter, getUniformIndices, getActiveUniforms, getUniformBlockIndex, getActiveUniformBlockParameter, getActiveUniformBlockName, uniformBlockBinding, createVertexArray, deleteVertexArray, isVertexArray, bindVertexArray, texImage2D, texSubImage2D, compressedTexImage2D, compressedTexSubImage2D, uniform1fv, uniform2fv, uniform3fv, uniform4fv, uniform1iv, uniform2iv, uniform3iv, uniform4iv, uniformMatrix2fv, uniformMatrix3fv, uniformMatrix4fv, readPixels; includes: WebGL2RenderingContext -> WebGLRenderingContextBase.
- `html/canvas/WebGLActiveInfo.idl` **[R10]** - WebGLActiveInfo: size, type, name.
- `html/canvas/WebGLBlendFuncExtended.idl` **[R10]** - WebGLBlendFuncExtended: no own parsed members.
- `html/canvas/WebGLBuffer.idl` **[R10]** - WebGLBuffer: no own parsed members.
- `html/canvas/WebGLClipCullDistance.idl` **[R10]** - WebGLClipCullDistance: no own parsed members.
- `html/canvas/WebGLColorBufferFloat.idl` **[R10]** - WebGLColorBufferFloat: no own parsed members.
- `html/canvas/WebGLCompressedTextureASTC.idl` **[R10]** - WebGLCompressedTextureASTC: getSupportedProfiles.
- `html/canvas/WebGLCompressedTextureETC.idl` **[R10]** - WebGLCompressedTextureETC: no own parsed members.
- `html/canvas/WebGLCompressedTextureETC1.idl` **[R10]** - WebGLCompressedTextureETC1: no own parsed members.
- `html/canvas/WebGLCompressedTexturePVRTC.idl` **[R10]** - WebGLCompressedTexturePVRTC: no own parsed members.
- `html/canvas/WebGLCompressedTextureS3TC.idl` **[R10]** - WebGLCompressedTextureS3TC: no own parsed members.
- `html/canvas/WebGLCompressedTextureS3TCsRGB.idl` **[R10]** - WebGLCompressedTextureS3TCsRGB: no own parsed members.
- `html/canvas/WebGLContextAttributes.idl` **[R10]** - dictionaries: WebGLContextAttributes; enums: WebGLContextSimulatedCreationFailure, WebGLPowerPreference.
- `html/canvas/WebGLContextEvent.idl` **[R10]** - WebGLContextEvent: constructor, statusMessage; dictionaries: WebGLContextEventInit.
- `html/canvas/WebGLDebugRendererInfo.idl` **[R10]** - WebGLDebugRendererInfo: no own parsed members.
- `html/canvas/WebGLDebugShaders.idl` **[R10]** - WebGLDebugShaders: getTranslatedShaderSource.
- `html/canvas/WebGLDepthTexture.idl` **[R10]** - WebGLDepthTexture: no own parsed members.
- `html/canvas/WebGLDrawBuffers.idl` **[R10]** - WebGLDrawBuffers: drawBuffersWEBGL.
- `html/canvas/WebGLDrawInstancedBaseVertexBaseInstance.idl` **[R10]** - WebGLDrawInstancedBaseVertexBaseInstance: drawArraysInstancedBaseInstanceWEBGL, drawElementsInstancedBaseVertexBaseInstanceWEBGL.
- `html/canvas/WebGLFramebuffer.idl` **[R10]** - WebGLFramebuffer: no own parsed members.
- `html/canvas/WebGLLoseContext.idl` **[R10]** - WebGLLoseContext: loseContext, restoreContext.
- `html/canvas/WebGLMultiDraw.idl` **[R10]** - WebGLMultiDraw: multiDrawArraysWEBGL, multiDrawArraysInstancedWEBGL, multiDrawElementsWEBGL, multiDrawElementsInstancedWEBGL.
- `html/canvas/WebGLMultiDrawInstancedBaseVertexBaseInstance.idl` **[R10]** - WebGLMultiDrawInstancedBaseVertexBaseInstance: multiDrawArraysInstancedBaseInstanceWEBGL, multiDrawElementsInstancedBaseVertexBaseInstanceWEBGL.
- `html/canvas/WebGLPolygonMode.idl` **[R10]** - WebGLPolygonMode: polygonModeWEBGL.
- `html/canvas/WebGLProgram.idl` **[R10]** - WebGLProgram: no own parsed members.
- `html/canvas/WebGLProvokingVertex.idl` **[R10]** - WebGLProvokingVertex: provokingVertexWEBGL.
- `html/canvas/WebGLQuery.idl` **[R10]** - WebGLQuery: no own parsed members.
- `html/canvas/WebGLRenderbuffer.idl` **[R10]** - WebGLRenderbuffer: no own parsed members.
- `html/canvas/WebGLRenderingContext.idl` **[R10]** - WebGLRenderingContext: no own parsed members; includes: WebGLRenderingContext -> WebGLRenderingContextBase.
- `html/canvas/WebGLRenderingContextBase.idl` **[R10]** - WebGLRenderingContextBase (mixin): canvas, drawingBufferWidth, drawingBufferHeight, activeTexture, attachShader, bindAttribLocation, bindBuffer, bindFramebuffer, bindRenderbuffer, bindTexture, blendColor, blendEquation, blendEquationSeparate, blendFunc, blendFuncSeparate, bufferData, bufferSubData, checkFramebufferStatus, clear, clearColor, clearDepth, clearStencil, colorMask, compileShader, texImage2D, texSubImage2D, compressedTexImage2D, compressedTexSubImage2D, copyTexImage2D, copyTexSubImage2D, createBuffer, createFramebuffer, createProgram, createRenderbuffer, createShader, createTexture, cullFace, deleteBuffer, deleteFramebuffer, deleteProgram, deleteRenderbuffer, deleteShader, deleteTexture, depthFunc, depthMask, depthRange, detachShader, disable, disableVertexAttribArray, drawArrays, drawElements, enable, enableVertexAttribArray, finish, flush, framebufferRenderbuffer, framebufferTexture2D, frontFace, generateMipmap, getActiveAttrib, getActiveUniform, getAttachedShaders, getAttribLocation, getBufferParameter, getContextAttributes, getError, getSupportedExtensions, getExtension, getFramebufferAttachmentParameter, getParameter, getProgramParameter, getProgramInfoLog, getRenderbufferParameter, getShaderParameter, getShaderInfoLog, getShaderPrecisionFormat, getShaderSource, getTexParameter, getUniform, getUniformLocation, getVertexAttrib, getVertexAttribOffset, hint, isBuffer, isContextLost, isEnabled, isFramebuffer, isProgram, isRenderbuffer, isShader, isTexture, lineWidth, linkProgram, makeXRCompatible, pixelStorei, polygonOffset, readPixels, renderbufferStorage, sampleCoverage, scissor, shaderSource, stencilFunc, stencilFuncSeparate, stencilMask, stencilMaskSeparate, stencilOp, stencilOpSeparate, texParameterf, texParameteri, uniform1f, uniform2f, uniform3f, uniform4f, uniform1i, uniform2i, uniform3i, uniform4i, uniform1fv, uniform2fv, uniform3fv, uniform4fv, uniform1iv, uniform2iv, uniform3iv, uniform4iv, uniformMatrix2fv, uniformMatrix3fv, uniformMatrix4fv, useProgram, validateProgram, vertexAttrib1f, vertexAttrib2f, vertexAttrib3f, vertexAttrib4f, vertexAttrib1fv, vertexAttrib2fv, vertexAttrib3fv, vertexAttrib4fv, vertexAttribPointer, viewport.
- `html/canvas/WebGLRenderSharedExponent.idl` **[R10]** - WebGLRenderSharedExponent: no own parsed members.
- `html/canvas/WebGLSampler.idl` **[R10]** - WebGLSampler: no own parsed members.
- `html/canvas/WebGLShader.idl` **[R10]** - WebGLShader: no own parsed members.
- `html/canvas/WebGLShaderPrecisionFormat.idl` **[R10]** - WebGLShaderPrecisionFormat: rangeMin, rangeMax, precision.
- `html/canvas/WebGLStencilTexturing.idl` **[R10]** - WebGLStencilTexturing: no own parsed members.
- `html/canvas/WebGLSync.idl` **[R10]** - WebGLSync: no own parsed members.
- `html/canvas/WebGLTexture.idl` **[R10]** - WebGLTexture: no own parsed members.
- `html/canvas/WebGLTimerQueryEXT.idl` **[R10]** - WebGLTimerQueryEXT: no own parsed members.
- `html/canvas/WebGLTransformFeedback.idl` **[R10]** - WebGLTransformFeedback: no own parsed members.
- `html/canvas/WebGLUniformLocation.idl` **[R10]** - WebGLUniformLocation: no own parsed members.
- `html/canvas/WebGLVertexArrayObject.idl` **[R10]** - WebGLVertexArrayObject: no own parsed members.
- `html/canvas/WebGLVertexArrayObjectOES.idl` **[R10]** - WebGLVertexArrayObjectOES: no own parsed members.
- `html/CaptionDisplaySettingsOptions.idl` **[B01, B02, B07, B08]** - dictionaries: CaptionDisplaySettingsOptions.
- `html/closewatcher/CloseWatcher.idl` **[D16]** - CloseWatcher: constructor, requestClose, close, destroy, oncancel, onclose; dictionaries: CloseWatcherOptions.
- `html/DOMFormData.idl` **[D13, D14]** - DOMFormData: constructor, append, delete, get, getAll, has, set; collections: iterable<USVString, FormDataEntryValue>;.
- `html/DOMTokenList.idl` **[D11]** - DOMTokenList: length, item, contains, add, remove, toggle, replace, supports, value; collections: iterable<DOMString>;.
- `html/DOMURL.idl` **[H13]** - DOMURL: constructor, parse, canParse, href, origin, protocol, username, password, host, hostname, port, pathname, hash, search, searchParams, toJSON, createObjectURL, revokeObjectURL.
- `html/HTMLAllCollection.idl` **[D19, B20]** - HTMLAllCollection: length, <indexed operation>, namedItem, item.
- `html/HTMLAnchorElement.idl` **[D11]** - HTMLAnchorElement: attributionSourceId, attributionDestination, attributionSourceNonce, charset, coords, download, hreflang, name, ping, rel, rev, shape, type, text, relList, referrerPolicy; includes: HTMLAnchorElement -> HyperlinkElementUtils, HTMLAnchorElement -> HTMLHyperlinkElementUtils.
- `html/HTMLAreaElement.idl` **[D09, D11]** - HTMLAreaElement: alt, coords, noHref, ping, rel, shape, download, referrerPolicy, relList; includes: HTMLAreaElement -> HyperlinkElementUtils, HTMLAreaElement -> HTMLHyperlinkElementUtils.
- `html/HTMLAttachmentElement.idl` **[D19, B20]** - HTMLAttachmentElement: file, uniqueIdentifier, saveButtonClientRect, getAttachmentIdentifier.
- `html/HTMLAudioElement.idl` **[B01, B02, B07, B08]** - HTMLAudioElement: no own parsed members.
- `html/HTMLBaseElement.idl` **[D11]** - HTMLBaseElement: href, target.
- `html/HTMLBodyElement+Compat.idl` **[D11]** - HTMLBodyElement (partial): onorientationchange.
- `html/HTMLBodyElement.idl` **[D11]** - HTMLBodyElement: aLink, background, bgColor, link, text, vLink, onblur, onerror, onfocus, onload, onresize, onscroll, onfocusin, onfocusout, onwebkitmouseforcechanged, onwebkitmouseforcedown, onwebkitmouseforcewillbegin, onwebkitmouseforceup; includes: HTMLBodyElement -> WindowEventHandlers.
- `html/HTMLBRElement.idl` **[D11]** - HTMLBRElement: clear.
- `html/HTMLButtonElement.idl` **[D11]** - HTMLButtonElement: disabled, form, formAction, formEnctype, formMethod, type, formNoValidate, formTarget, name, value, commandForElement, command, willValidate, validity, validationMessage, checkValidity, reportValidity, setCustomValidity, labels; includes: HTMLButtonElement -> PopoverInvokerElement.
- `html/HTMLCanvasElement.idl` **[R01, R02, R03, R04, R05, R06, R07, R08]** - HTMLCanvasElement: width, height, getContext, toDataURL, toBlob, transferControlToOffscreen, captureStream.
- `html/HTMLCollection.idl` **[D11]** - HTMLCollection: length, item, namedItem.
- `html/HTMLDataElement.idl` **[D11]** - HTMLDataElement: value.
- `html/HTMLDataListElement.idl` **[D11]** - HTMLDataListElement: options.
- `html/HTMLDetailsElement.idl` **[D11]** - HTMLDetailsElement: name, open.
- `html/HTMLDialogElement.idl` **[D16]** - HTMLDialogElement: open, returnValue, closedBy, show, showModal, close, requestClose.
- `html/HTMLDirectoryElement.idl` **[D19, B20]** - HTMLDirectoryElement: compact.
- `html/HTMLDivElement.idl` **[D11]** - HTMLDivElement: align.
- `html/HTMLDListElement.idl` **[D11]** - HTMLDListElement: compact.
- `html/HTMLDocument.idl` **[D11]** - HTMLDocument: <indexed operation>.
- `html/HTMLElement+CSSOMView.idl` **[D11]** - HTMLElement (partial): offsetParent, offsetTop, offsetLeft, offsetWidth, offsetHeight.
- `html/HTMLElement.idl` **[D11]** - HTMLElement: constructor, title, lang, translate, dir, cue, cuebackground, hidden, click, accessKey, accessKeyLabel, draggable, spellcheck, autocapitalize, innerText, attachInternals, inert, headingOffset, headingReset, showPopover, hidePopover, togglePopover, popover, outerText, autocorrect, webkitdropzone, writingSuggestions; dictionaries: ShowPopoverOptions, TogglePopoverOptions; includes: HTMLElement -> GlobalEventHandlers, HTMLElement -> DocumentAndElementEventHandlers, HTMLElement -> ElementContentEditable, HTMLElement -> HTMLOrSVGOrMathMLElement, HTMLElement -> ElementCSSInlineStyle.
- `html/HTMLEmbedElement.idl` **[D19, B20]** - HTMLEmbedElement: align, height, name, src, type, width, getSVGDocument.
- `html/HTMLFieldSetElement.idl` **[D11]** - HTMLFieldSetElement: disabled, form, name, type, elements, willValidate, validity, validationMessage, checkValidity, reportValidity, setCustomValidity.
- `html/HTMLFontElement.idl` **[D19, B20]** - HTMLFontElement: color, face, size.
- `html/HTMLFormControlsCollection.idl` **[D13, D14]** - HTMLFormControlsCollection: <indexed operation>, namedItem.
- `html/HTMLFormElement.idl` **[D11]** - HTMLFormElement: acceptCharset, action, autocomplete, enctype, encoding, method, name, noValidate, target, rel, relList, elements, length, <indexed operation>, submit, requestSubmit, reset, checkValidity, reportValidity.
- `html/HTMLFrameElement.idl` **[D19, B20]** - HTMLFrameElement: name, scrolling, src, frameBorder, longDesc, noResize, contentDocument, contentWindow, marginHeight, marginWidth.
- `html/HTMLFrameSetElement.idl` **[D19, B20]** - HTMLFrameSetElement: cols, rows, onblur, onerror, onfocus, onload, onresize, onscroll, onfocusin, onfocusout, <indexed operation>; includes: HTMLFrameSetElement -> WindowEventHandlers.
- `html/HTMLHeadElement.idl` **[D11]** - HTMLHeadElement: no own parsed members.
- `html/HTMLHeadingElement.idl` **[D11]** - HTMLHeadingElement: align.
- `html/HTMLHRElement.idl` **[D11]** - HTMLHRElement: align, color, noShade, size, width.
- `html/HTMLHtmlElement.idl` **[D11]** - HTMLHtmlElement: version.
- `html/HTMLHyperlinkElementUtils.idl` **[D09, D11]** - HTMLHyperlinkElementUtils (mixin): href, target.
- `html/HTMLIFrameElement.idl` **[D19, B20]** - HTMLIFrameElement: src, srcdoc, name, sandbox, allow, allowFullscreen, width, height, referrerPolicy, loading, contentDocument, contentWindow, getSVGDocument, align, scrolling, frameBorder, longDesc, marginHeight, marginWidth; enums: HTMLIFrameElementLoadingValues.
- `html/HTMLImageElement+CSSOMView.idl` **[R09]** - HTMLImageElement (partial): x, y.
- `html/HTMLImageElement.idl` **[R09]** - HTMLImageElement: alt, src, srcset, sizes, crossOrigin, useMap, isMap, controls, width, height, naturalHeight, naturalWidth, complete, currentSrc, referrerPolicy, decoding, loading, decode, name, lowsrc, align, hspace, vspace, longDesc, border, fetchPriority, attachmentIdentifier; enums: HTMLImageElementLoadingValues.
- `html/HTMLInputElement.idl` **[D15]** - HTMLInputElement: accept, alpha, alt, autocomplete, defaultChecked, checked, colorSpace, dirName, disabled, form, files, formAction, formEnctype, formMethod, formNoValidate, formTarget, height, indeterminate, list, max, minLength, maxLength, min, multiple, name, pattern, placeholder, readOnly, required, size, src, step, switch, type, defaultValue, value, valueAsDate, valueAsNumber, width, stepUp, stepDown, willValidate, validity, validationMessage, checkValidity, reportValidity, setCustomValidity, labels, select, selectionStart, selectionEnd, selectionDirection, setRangeText, setSelectionRange, showPicker, align, useMap, autofillAvailable, autofilled, lastChangeWasUserEdit, autofillVisibility, autofillButtonType, autofillSpellcheck, autofilledAndObscured, autofilledAndViewable, capture; enums: AutofillButtonType, AutofillVisibility; includes: HTMLInputElement -> PopoverInvokerElement.
- `html/HTMLLabelElement.idl` **[D11]** - HTMLLabelElement: form, htmlFor, control.
- `html/HTMLLegendElement.idl` **[D11]** - HTMLLegendElement: form, align.
- `html/HTMLLIElement.idl` **[D11]** - HTMLLIElement: type, value.
- `html/HTMLLinkElement.idl` **[D12]** - HTMLLinkElement: href, crossOrigin, rel, as, relList, media, integrity, hreflang, type, sizes, imageSrcset, imageSizes, referrerPolicy, blocking, disabled, fetchPriority, charset, rev, target; includes: HTMLLinkElement -> LinkStyle.
- `html/HTMLMapElement.idl` **[D11]** - HTMLMapElement: areas, name.
- `html/HTMLMarqueeElement.idl` **[D19, B20]** - HTMLMarqueeElement: start, stop, behavior, bgColor, direction, height, hspace, loop, scrollAmount, scrollDelay, trueSpeed, vspace, width.
- `html/HTMLMediaElement+AudioOutput.idl` **[B01, B02, B07, B08]** - HTMLMediaElement (partial): sinkId, setSinkId.
- `html/HTMLMediaElement.idl` **[B01, B02, B07, B08]** - HTMLMediaElement: error, src, srcObject, currentSrc, crossOrigin, networkState, preload, buffered, load, canPlayType, readyState, seeking, currentTime, fastSeek, duration, getStartDate, paused, defaultPlaybackRate, playbackRate, preservesPitch, played, seekable, ended, autoplay, loop, play, pause, controls, volume, muted, defaultMuted, webkitAudioDecodedByteCount, webkitVideoDecodedByteCount, webkitKeys, webkitSetMediaKeys, mediaKeys, onencrypted, onwaitingforkey, setMediaKeys, addTextTrack, audioTracks, textTracks, videoTracks, mediaGroup, controller, getVideoPlaybackQuality, webkitShowPlaybackTargetPicker, webkitCurrentPlaybackTargetIsWireless, controlsHost.
- `html/HTMLMenuElement.idl` **[D11]** - HTMLMenuElement: compact.
- `html/HTMLMetaElement.idl` **[D11]** - HTMLMetaElement: content, httpEquiv, media, name, scheme.
- `html/HTMLMeterElement.idl` **[D11]** - HTMLMeterElement: value, min, max, low, high, optimum, labels.
- `html/HTMLModElement.idl` **[D11]** - HTMLModElement: cite, dateTime.
- `html/HTMLObjectElement.idl` **[D19, B20]** - HTMLObjectElement: data, type, name, form, width, height, contentDocument, contentWindow, getSVGDocument, willValidate, validity, validationMessage, checkValidity, reportValidity, setCustomValidity, align, archive, code, declare, hspace, standby, vspace, codeBase, codeType, useMap, border.
- `html/HTMLOListElement.idl` **[D11]** - HTMLOListElement: compact, start, reversed, type.
- `html/HTMLOptGroupElement.idl` **[D11]** - HTMLOptGroupElement: disabled, label.
- `html/HTMLOptionElement.idl` **[D11]** - HTMLOptionElement: disabled, form, label, defaultSelected, selected, value, text, index.
- `html/HTMLOptionsCollection.idl` **[D13, D14]** - HTMLOptionsCollection: length, <indexed operation>, add, remove, selectedIndex, item, namedItem.
- `html/HTMLOrSVGOrMathMLElement.idl` **[D11]** - HTMLOrSVGOrMathMLElement (mixin): dataset, nonce, autofocus, tabIndex, focus, blur.
- `html/HTMLOutputElement.idl` **[D11]** - HTMLOutputElement: constructor, htmlFor, form, name, type, defaultValue, value, willValidate, validity, validationMessage, checkValidity, reportValidity, setCustomValidity, labels.
- `html/HTMLParagraphElement.idl` **[D11]** - HTMLParagraphElement: align.
- `html/HTMLParamElement.idl` **[D19, B20]** - HTMLParamElement: name, type, value, valueType.
- `html/HTMLPictureElement.idl` **[D11]** - HTMLPictureElement: no own parsed members.
- `html/HTMLPreElement.idl` **[D11]** - HTMLPreElement: width.
- `html/HTMLProgressElement.idl` **[D11]** - HTMLProgressElement: value, max, position, labels.
- `html/HTMLQuoteElement.idl` **[D11]** - HTMLQuoteElement: cite.
- `html/HTMLScriptElement.idl` **[D19, B20]** - HTMLScriptElement: text, innerText, textContent, htmlFor, event, charset, async, defer, src, type, crossOrigin, noModule, integrity, referrerPolicy, fetchPriority, blocking, supports.
- `html/HTMLSelectedContentElement.idl` **[D11]** - HTMLSelectedContentElement: no own parsed members.
- `html/HTMLSelectElement.idl` **[D15]** - HTMLSelectElement: autocomplete, disabled, form, multiple, name, required, size, type, options, length, item, namedItem, add, remove, <indexed operation>, selectedOptions, selectedIndex, value, willValidate, validity, validationMessage, checkValidity, reportValidity, setCustomValidity, showPicker, labels.
- `html/HTMLSlotElement.idl` **[D11]** - HTMLSlotElement: name, assignedNodes, assignedElements, assign; dictionaries: AssignedNodesOptions.
- `html/HTMLSourceElement.idl` **[D11]** - HTMLSourceElement: src, type, srcset, sizes, media, width, height, attachmentIdentifier.
- `html/HTMLSpanElement.idl` **[D11]** - HTMLSpanElement: no own parsed members.
- `html/HTMLStyleElement.idl` **[D12]** - HTMLStyleElement: disabled, media, type, blocking; includes: HTMLStyleElement -> LinkStyle.
- `html/HTMLTableCaptionElement.idl` **[B01, B02, B07, B08]** - HTMLTableCaptionElement: align.
- `html/HTMLTableCellElement.idl` **[D10]** - HTMLTableCellElement: cellIndex, align, axis, bgColor, ch, chOff, colSpan, rowSpan, headers, height, noWrap, vAlign, width, abbr, scope.
- `html/HTMLTableColElement.idl` **[D10]** - HTMLTableColElement: align, ch, chOff, span, vAlign, width.
- `html/HTMLTableElement.idl` **[D10]** - HTMLTableElement: caption, createCaption, deleteCaption, tHead, createTHead, deleteTHead, tFoot, createTFoot, deleteTFoot, tBodies, createTBody, rows, insertRow, deleteRow, align, border, frame, rules, summary, width, bgColor, cellPadding, cellSpacing.
- `html/HTMLTableRowElement.idl` **[D10]** - HTMLTableRowElement: rowIndex, sectionRowIndex, cells, align, bgColor, ch, chOff, vAlign, insertCell, deleteCell.
- `html/HTMLTableSectionElement.idl` **[D10]** - HTMLTableSectionElement: rows, insertRow, deleteRow, align, ch, chOff, vAlign.
- `html/HTMLTemplateElement.idl` **[D11]** - HTMLTemplateElement: content, shadowRootMode, shadowRootDelegatesFocus, shadowRootSlotAssignment, shadowRootClonable, shadowRootSerializable, shadowRootCustomElementRegistry, shadowRootReferenceTarget.
- `html/HTMLTextAreaElement.idl` **[D11]** - HTMLTextAreaElement: dirName, disabled, form, minLength, maxLength, name, placeholder, readOnly, required, rows, cols, wrap, type, defaultValue, value, textLength, willValidate, validity, validationMessage, checkValidity, reportValidity, setCustomValidity, labels, select, selectionStart, selectionEnd, selectionDirection, setRangeText, setSelectionRange, autocomplete, lastChangeWasUserEdit.
- `html/HTMLTimeElement.idl` **[D11]** - HTMLTimeElement: dateTime.
- `html/HTMLTitleElement.idl` **[D11]** - HTMLTitleElement: text.
- `html/HTMLTrackElement.idl` **[B01, B02, B07, B08]** - HTMLTrackElement: kind, src, srclang, label, default, readyState, track.
- `html/HTMLUListElement.idl` **[D11]** - HTMLUListElement: compact, type.
- `html/HTMLUnknownElement.idl` **[D11]** - HTMLUnknownElement: no own parsed members.
- `html/HTMLVideoElement+CaptionDisplaySettings.idl` **[B01, B02, B07, B08]** - HTMLVideoElement (partial): showCaptionDisplaySettings.
- `html/HTMLVideoElement+RequestVideoFrameCallback.idl` **[B01, B02, B07, B08]** - HTMLVideoElement (partial): requestVideoFrameCallback, cancelVideoFrameCallback.
- `html/HTMLVideoElement.idl` **[B01, B02, B07, B08]** - HTMLVideoElement: width, height, videoWidth, videoHeight, poster, playsInline, webkitSupportsFullscreen, webkitDisplayingFullscreen, webkitEnterFullscreen, webkitExitFullscreen, webkitEnterFullScreen, webkitExitFullScreen, webkitWirelessVideoPlaybackDisabled, webkitDecodedFrameCount, webkitDroppedFrameCount, getVideoPlaybackQuality, webkitSupportsPresentationMode, webkitPresentationMode, webkitSetPresentationMode; enums: VideoPresentationMode.
- `html/HyperlinkElementUtils.idl` **[D09, D11]** - HyperlinkElementUtils (mixin): origin, protocol, username, password, host, hostname, port, pathname, search, hash.
- `html/ImageBitmap.idl` **[R01, R02, R03, R04, R05, R06, R07, R08]** - ImageBitmap: width, height, close.
- `html/ImageBitmapOptions.idl` **[R01, R02, R03, R04, R05, R06, R07, R08]** - dictionaries: ImageBitmapOptions; enums: ColorSpaceConversion, ImageOrientation, PremultiplyAlpha, ResizeQuality.
- `html/ImageData.idl` **[R01, R02, R03, R04, R05, R06, R07, R08]** - ImageData: constructor, width, height, data, colorSpace, pixelFormat.
- `html/ImageDataPixelFormat.idl` **[R01, R02, R03, R04, R05, R06, R07, R08]** - enums: ImageDataPixelFormat.
- `html/ImageDataSettings.idl` **[R01, R02, R03, R04, R05, R06, R07, R08]** - dictionaries: ImageDataSettings.
- `html/MediaController.idl` **[B01, B02, B07, B08]** - MediaController: constructor, buffered, seekable, duration, currentTime, paused, played, playbackState, play, pause, unpause, defaultPlaybackRate, playbackRate, volume, muted.
- `html/MediaEncryptedEvent.idl` **[B01, B02, B07, B08]** - MediaEncryptedEvent: constructor, initDataType, initData; dictionaries: MediaEncryptedEventInit.
- `html/MediaError.idl` **[B01, B02, B07, B08]** - MediaError: code, message.
- `html/OffscreenCanvas.idl` **[R01, R02, R03, R04, R05, R06, R07, R08]** - OffscreenCanvas: constructor, width, height, getContext, transferToImageBitmap, convertToBlob; dictionaries: ImageEncodeOptions; enums: OffscreenRenderingContextType.
- `html/Origin.idl` **[I12, B21]** - Origin: constructor, from, opaque, isSameOrigin, isSameSite.
- `html/PopoverInvokerElement.idl` **[D16]** - PopoverInvokerElement (mixin): popoverTargetElement, popoverTargetAction.
- `html/RadioNodeList.idl` **[D11]** - RadioNodeList: value.
- `html/ResolvedCaptionDisplaySettingsOptions.idl` **[B01, B02, B07, B08]** - dictionaries: ResolvedCaptionDisplaySettingsOptions; enums: ResolvedCaptionDisplaySettingsOptionsXPositionArea, ResolvedCaptionDisplaySettingsOptionsYPositionArea.
- `html/SubmitEvent.idl` **[D13, D14]** - SubmitEvent: constructor, submitter; dictionaries: SubmitEventInit.
- `html/TextMetrics.idl` **[R01, R02, R03, R04, R05, R06, R07, R08]** - TextMetrics: width, actualBoundingBoxLeft, actualBoundingBoxRight, fontBoundingBoxAscent, fontBoundingBoxDescent, actualBoundingBoxAscent, actualBoundingBoxDescent, emHeightAscent, emHeightDescent, hangingBaseline, alphabeticBaseline, ideographicBaseline.
- `html/TimeRanges.idl` **[B01, B02, B07, B08]** - TimeRanges: length, start, end.
- `html/track/AudioTrack.idl` **[B01, B02, B07, B08]** - AudioTrack: id, kind, label, language, enabled, configuration.
- `html/track/AudioTrackConfiguration.idl` **[B01, B02, B07, B08]** - AudioTrackConfiguration: codec, sampleRate, numberOfChannels, bitrate.
- `html/track/AudioTrackList.idl` **[B01, B02, B07, B08]** - AudioTrackList: length, item, getTrackById, onchange, onaddtrack, onremovetrack.
- `html/track/DataCue.idl` **[B01, B02, B07, B08]** - DataCue: constructor, value, type, data.
- `html/track/TextTrack.idl` **[B01, B02, B07, B08]** - TextTrack: kind, label, language, id, inBandMetadataTrackDispatchType, mode, cues, activeCues, addCue, removeCue, oncuechange; enums: TextTrackKind, TextTrackMode.
- `html/track/TextTrackCue.idl` **[B01, B02, B07, B08]** - TextTrackCue: track, id, startTime, endTime, pauseOnExit, onenter, onexit, constructor, getCueAsHTML.
- `html/track/TextTrackCueGeneric.idl` **[B01, B02, B07, B08]** - TextTrackCueGeneric: no own parsed members.
- `html/track/TextTrackCueList.idl` **[B01, B02, B07, B08]** - TextTrackCueList: length, item, getCueById.
- `html/track/TextTrackList.idl` **[B01, B02, B07, B08]** - TextTrackList: length, item, getTrackById, onaddtrack, onchange, onremovetrack.
- `html/track/TrackEvent.idl` **[B01, B02, B07, B08]** - TrackEvent: constructor, track; dictionaries: TrackEventInit.
- `html/track/VideoTrack.idl` **[B01, B02, B07, B08]** - VideoTrack: id, kind, label, language, selected, configuration.
- `html/track/VideoTrackConfiguration.idl` **[B01, B02, B07, B08]** - VideoTrackConfiguration: codec, width, height, bitrate, framerate, colorSpace.
- `html/track/VideoTrackList.idl` **[B01, B02, B07, B08]** - VideoTrackList: length, item, getTrackById, selectedIndex, onchange, onaddtrack, onremovetrack.
- `html/track/VTTCue.idl` **[B01, B02, B07, B08]** - VTTCue: constructor, region, vertical, snapToLines, line, lineAlign, position, positionAlign, size, align, text, getCueAsHTML; enums: AlignSetting, AutoKeyword, DirectionSetting, LineAlignSetting, PositionAlignSetting.
- `html/track/VTTRegion.idl` **[B01, B02, B07, B08]** - VTTRegion: constructor, id, width, lines, regionAnchorX, regionAnchorY, viewportAnchorX, viewportAnchorY, scroll; enums: ScrollSetting.
- `html/track/VTTRegionList.idl` **[B01, B02, B07, B08]** - VTTRegionList: length, item, getRegionById.
- `html/URLSearchParams.idl` **[D19, B20]** - URLSearchParams: constructor, size, append, delete, get, getAll, has, set, sort; collections: iterable<USVString, USVString>;.
- `html/UserActivation.idl` **[I12, B21]** - UserActivation: hasBeenActive, isActive.
- `html/ValidityState.idl` **[D11]** - ValidityState: valueMissing, typeMismatch, patternMismatch, tooShort, tooLong, rangeUnderflow, rangeOverflow, stepMismatch, badInput, customError, valid.
- `html/VideoFrameMetadata.idl` **[B01, B02, B07, B08]** - dictionaries: VideoFrameMetadata.
- `html/VideoFrameRequestCallback.idl` **[B01, B02, B07, B08]** - callbacks: VideoFrameRequestCallback.
- `html/VoidCallback.idl` **[D11]** - callbacks: VoidCallback.
- `html/WebKitMediaKeyError.idl` **[B01, B02, B07, B08]** - WebKitMediaKeyError: code, systemCode.
### inspector

- `inspector/CommandLineAPIHost.idl` **[H08, C25]** - CommandLineAPIHost: copyText, inspect, inspectedObject, getEventListeners, gatherRTCLogs, storageId; dictionaries: ListenerEntry.
- `inspector/InspectorAuditAccessibilityObject.idl` **[H08, C25]** - InspectorAuditAccessibilityObject: getElementsByComputedRole, getActiveDescendant, getChildNodes, getComputedProperties, getControlledNodes, getFlowedNodes, getMouseEventNode, getOwnedNodes, getParentNode, getSelectedChildNodes; dictionaries: ComputedProperties.
- `inspector/InspectorAuditDOMObject.idl` **[H08, C25]** - InspectorAuditDOMObject: hasEventListeners, simulateUserInteraction.
- `inspector/InspectorAuditResourcesObject.idl` **[H08, C25]** - InspectorAuditResourcesObject: getResources, getResourceContent; dictionaries: Resource, ResourceContent.
- `inspector/InspectorFrontendHost.idl` **[H08, C25]** - InspectorFrontendHost: loaded, closeWindow, reopen, reset, bringToFront, inspectedURLChanged, supportsShowCertificate, showCertificate, setZoomFactor, zoomFactor, setForcedAppearance, userInterfaceLayoutDirection, supportsDockSide, requestSetDockSide, setAttachedWindowHeight, setAttachedWindowWidth, setSheetRect, startWindowDrag, moveWindowBy, isRemote, localizedStringsURL, backendCommandsURL, inspectionLevel, debuggableInfo, copyText, killText, openURLExternally, revealFileExternally, canSave, save, canLoad, load, canPickColorFromScreen, pickColorFromScreen, platform, platformVersionName, showContextMenu, dispatchEventAsContextMenuEvent, sendMessageToBackend, unbufferedLog, isUnderTest, beep, inspectInspector, isBeingInspected, setAllowsInspectingInspector, engineeringSettingsAllowed, supportsDiagnosticLogging, logDiagnosticEvent, supportsWebExtensions, didShowExtensionTab, didHideExtensionTab, didNavigateExtensionTab, inspectedPageDidNavigate, evaluateScriptInExtensionTab, getPath, getCurrentX, getCurrentY, setPath; dictionaries: ContextMenuItem, DebuggableInfo, SaveData; enums: SaveMode.
- `inspector/RTCLogsCallback.idl` **[H08, C25]** - dictionaries: RTCLogs; callbacks: RTCLogsCallback.
### loader

- `loader/COEPInheritenceViolationReportBody.idl` **[H10, B25]** - COEPInheritenceViolationReportBody: disposition, type, blockedURL, toJSON.
- `loader/CORPViolationReportBody.idl` **[H10, B25]** - CORPViolationReportBody: disposition, type, blockedURL, destination, toJSON.
### mathml

- `mathml/MathMLElement.idl` **[S13]** - MathMLElement: no own parsed members; includes: MathMLElement -> GlobalEventHandlers, MathMLElement -> DocumentAndElementEventHandlers, MathMLElement -> HTMLOrSVGOrMathMLElement, MathMLElement -> ElementCSSInlineStyle.
- `mathml/MathMLMathElement.idl` **[S13]** - MathMLMathElement: no own parsed members.
### Modules/airplay

- `Modules/airplay/WebKitPlaybackTargetAvailabilityEvent.idl` **[B07]** - WebKitPlaybackTargetAvailabilityEvent: constructor, availability; dictionaries: WebKitPlaybackTargetAvailabilityEventInit.
### Modules/applepay

- `Modules/applepay/ApplePayAutomaticReloadPaymentRequest.idl` **[B21]** - dictionaries: ApplePayAutomaticReloadPaymentRequest.
- `Modules/applepay/ApplePayCancelEvent.idl` **[B21]** - ApplePayCancelEvent: sessionError.
- `Modules/applepay/ApplePayContactField.idl` **[B21]** - enums: ApplePayContactField.
- `Modules/applepay/ApplePayCouponCodeChangedEvent.idl` **[B21]** - ApplePayCouponCodeChangedEvent: couponCode.
- `Modules/applepay/ApplePayCouponCodeDetails.idl` **[B21]** - dictionaries: ApplePayCouponCodeDetails.
- `Modules/applepay/ApplePayCouponCodeUpdate.idl` **[B21]** - dictionaries: ApplePayCouponCodeUpdate.
- `Modules/applepay/ApplePayDateComponents.idl` **[B21]** - dictionaries: ApplePayDateComponents.
- `Modules/applepay/ApplePayDateComponentsRange.idl` **[B21]** - dictionaries: ApplePayDateComponentsRange.
- `Modules/applepay/ApplePayDeferredPaymentRequest.idl` **[B21]** - dictionaries: ApplePayDeferredPaymentRequest.
- `Modules/applepay/ApplePayDetailsUpdateBase.idl` **[B21]** - dictionaries: ApplePayDetailsUpdateBase.
- `Modules/applepay/ApplePayDisbursementRequest.idl` **[B21]** - dictionaries: ApplePayDisbursementRequest.
- `Modules/applepay/ApplePayError.idl` **[B21]** - ApplePayError: constructor, code, contactField, message, domain; enums: ApplePayErrorDomain.
- `Modules/applepay/ApplePayErrorCode.idl` **[B21]** - enums: ApplePayErrorCode.
- `Modules/applepay/ApplePayErrorContactField.idl` **[B21]** - enums: ApplePayErrorContactField.
- `Modules/applepay/ApplePayFeature.idl` **[B21]** - enums: ApplePayFeature.
- `Modules/applepay/ApplePayInstallmentConfiguration.idl` **[B21]** - dictionaries: ApplePayInstallmentConfiguration.
- `Modules/applepay/ApplePayInstallmentItem.idl` **[B21]** - dictionaries: ApplePayInstallmentItem.
- `Modules/applepay/ApplePayInstallmentItemType.idl` **[B21]** - enums: ApplePayInstallmentItemType.
- `Modules/applepay/ApplePayInstallmentRetailChannel.idl` **[B21]** - enums: ApplePayInstallmentRetailChannel.
- `Modules/applepay/ApplePayLaterAvailability.idl` **[B21]** - enums: ApplePayLaterAvailability.
- `Modules/applepay/ApplePayLineItem.idl` **[B21]** - dictionaries: ApplePayLineItem; enums: ApplePayDisbursementLineItemType, ApplePayLineItemType.
- `Modules/applepay/ApplePayMerchantCapability.idl` **[B21]** - enums: ApplePayMerchantCapability.
- `Modules/applepay/ApplePayPayment.idl` **[B21]** - dictionaries: ApplePayPayment, ApplePayPaymentToken.
- `Modules/applepay/ApplePayPaymentAuthorizationResult.idl` **[B21]** - dictionaries: ApplePayPaymentAuthorizationResult.
- `Modules/applepay/ApplePayPaymentAuthorizedEvent.idl` **[B21]** - ApplePayPaymentAuthorizedEvent: payment.
- `Modules/applepay/ApplePayPaymentContact.idl` **[B21]** - dictionaries: ApplePayPaymentContact.
- `Modules/applepay/ApplePayPaymentMethod.idl` **[B21]** - dictionaries: ApplePayPaymentMethod.
- `Modules/applepay/ApplePayPaymentMethodSelectedEvent.idl` **[B21]** - ApplePayPaymentMethodSelectedEvent: paymentMethod.
- `Modules/applepay/ApplePayPaymentMethodType.idl` **[B21]** - enums: ApplePayPaymentMethodType.
- `Modules/applepay/ApplePayPaymentMethodUpdate.idl` **[B21]** - dictionaries: ApplePayPaymentMethodUpdate.
- `Modules/applepay/ApplePayPaymentOrderDetails.idl` **[B21]** - dictionaries: ApplePayPaymentOrderDetails.
- `Modules/applepay/ApplePayPaymentPass.idl` **[B21]** - dictionaries: ApplePayPaymentPass; enums: ApplePayPaymentPassActivationState.
- `Modules/applepay/ApplePayPaymentRequest.idl` **[B21]** - dictionaries: ApplePayPaymentRequest; enums: ApplePayShippingType.
- `Modules/applepay/ApplePayPaymentTiming.idl` **[B21]** - enums: ApplePayPaymentTiming.
- `Modules/applepay/ApplePayPaymentTokenContext.idl` **[B21]** - dictionaries: ApplePayPaymentTokenContext.
- `Modules/applepay/ApplePayRecurringPaymentDateUnit.idl` **[B21]** - enums: ApplePayRecurringPaymentDateUnit.
- `Modules/applepay/ApplePayRecurringPaymentRequest.idl` **[B21]** - dictionaries: ApplePayRecurringPaymentRequest.
- `Modules/applepay/ApplePayRequestBase.idl` **[B21]** - dictionaries: ApplePayRequestBase.
- `Modules/applepay/ApplePaySession.idl` **[B21]** - ApplePaySession: constructor, supportsVersion, canMakePayments, canMakePaymentsWithActiveCard, openPaymentSetup, begin, abort, completeMerchantValidation, completeShippingMethodSelection, completeShippingContactSelection, completePaymentMethodSelection, completeCouponCodeChange, completePayment, onvalidatemerchant, onpaymentmethodselected, onpaymentauthorized, onshippingmethodselected, onshippingcontactselected, oncouponcodechanged, oncancel.
- `Modules/applepay/ApplePaySessionError.idl` **[B21]** - dictionaries: ApplePaySessionError.
- `Modules/applepay/ApplePaySetup.idl` **[B21]** - ApplePaySetup: constructor, getSetupFeatures, begin.
- `Modules/applepay/ApplePaySetupConfiguration.idl` **[B21]** - dictionaries: ApplePaySetupConfiguration.
- `Modules/applepay/ApplePaySetupFeature.idl` **[B21]** - ApplePaySetupFeature: type, state, supportsInstallments.
- `Modules/applepay/ApplePaySetupFeatureState.idl` **[B21]** - enums: ApplePaySetupFeatureState.
- `Modules/applepay/ApplePaySetupFeatureType.idl` **[B21]** - enums: ApplePaySetupFeatureType.
- `Modules/applepay/ApplePayShippingContactEditingMode.idl` **[B21]** - enums: ApplePayShippingContactEditingMode.
- `Modules/applepay/ApplePayShippingContactSelectedEvent.idl` **[B21]** - ApplePayShippingContactSelectedEvent: shippingContact.
- `Modules/applepay/ApplePayShippingContactUpdate.idl` **[B21]** - dictionaries: ApplePayShippingContactUpdate.
- `Modules/applepay/ApplePayShippingMethod.idl` **[B21]** - dictionaries: ApplePayShippingMethod.
- `Modules/applepay/ApplePayShippingMethodSelectedEvent.idl` **[B21]** - ApplePayShippingMethodSelectedEvent: shippingMethod.
- `Modules/applepay/ApplePayShippingMethodUpdate.idl` **[B21]** - dictionaries: ApplePayShippingMethodUpdate.
- `Modules/applepay/ApplePayValidateMerchantEvent.idl` **[B21]** - ApplePayValidateMerchantEvent: validationURL.
- `Modules/applepay/paymentrequest/ApplePayModifier.idl` **[B21]** - dictionaries: ApplePayModifier.
- `Modules/applepay/paymentrequest/ApplePayPaymentCompleteDetails.idl` **[B21]** - dictionaries: ApplePayPaymentCompleteDetails.
- `Modules/applepay/paymentrequest/ApplePayRequest.idl` **[B21]** - dictionaries: ApplePayRequest.
### Modules/applepay-ams-ui

- `Modules/applepay-ams-ui/ApplePayAMSUIRequest.idl` **[B21]** - dictionaries: ApplePayAMSUIRequest.
### Modules/async-clipboard

- `Modules/async-clipboard/Clipboard.idl` **[T09]** - Clipboard: read, readText, write, writeText.
- `Modules/async-clipboard/ClipboardItem.idl` **[T09]** - ClipboardItem: constructor, types, getType, supports, presentationStyle; dictionaries: ClipboardItemOptions; enums: PresentationStyle.
- `Modules/async-clipboard/Navigator+Clipboard.idl` **[T09]** - Navigator (partial): clipboard.
### Modules/audiosession

- `Modules/audiosession/DOMAudioSession.idl` **[B01]** - DOMAudioSession: type, state, onstatechange; enums: DOMAudioSessionState, DOMAudioSessionType.
- `Modules/audiosession/Navigator+AudioSession.idl` **[B01]** - Navigator (partial): audioSession.
### Modules/badge

- `Modules/badge/NavigatorBadge.idl` **[B09]** - NavigatorBadge (mixin): setAppBadge, clearAppBadge.
### Modules/beacon

- `Modules/beacon/Navigator+Beacon.idl` **[B18]** - Navigator (partial): sendBeacon.
### Modules/cache

- `Modules/cache/CacheQueryOptions.idl` **[B19]** - dictionaries: CacheQueryOptions.
- `Modules/cache/DOMCache.idl` **[B19]** - DOMCache: match, matchAll, add, addAll, put, delete, keys.
- `Modules/cache/DOMCacheStorage.idl` **[B19]** - DOMCacheStorage: match, has, open, delete, keys.
- `Modules/cache/MultiCacheQueryOptions.idl` **[B19]** - dictionaries: MultiCacheQueryOptions.
- `Modules/cache/WindowOrWorkerGlobalScope+Caches.idl` **[B19]** - WindowOrWorkerGlobalScope (partial) (mixin): caches.
### Modules/compression

- `Modules/compression/CompressionStream.idl` **[H12]** - CompressionStream: constructor; includes: CompressionStream -> GenericTransformStream.
- `Modules/compression/CompressionStreamEncoder.idl` **[H12]** - CompressionStreamEncoder: constructor, encode, flush.
- `Modules/compression/DecompressionStream.idl` **[H12]** - DecompressionStream: constructor; includes: DecompressionStream -> GenericTransformStream.
- `Modules/compression/DecompressionStreamDecoder.idl` **[H12]** - DecompressionStreamDecoder: constructor, decode, flush, didDetectExtraBytes.
### Modules/contact-picker

- `Modules/contact-picker/ContactInfo.idl` **[B14]** - dictionaries: ContactInfo.
- `Modules/contact-picker/ContactProperty.idl` **[B14]** - enums: ContactProperty.
- `Modules/contact-picker/ContactsManager.idl` **[B14]** - ContactsManager: getProperties, select.
- `Modules/contact-picker/ContactsSelectOptions.idl` **[B14]** - dictionaries: ContactsSelectOptions.
- `Modules/contact-picker/Navigator+Contacts.idl` **[B14]** - Navigator (partial): contacts.
### Modules/cookie-store

- `Modules/cookie-store/CookieChangeEvent.idl` **[B19]** - CookieChangeEvent: constructor, changed, deleted.
- `Modules/cookie-store/CookieChangeEventInit.idl` **[B19]** - dictionaries: CookieChangeEventInit.
- `Modules/cookie-store/CookieInit.idl` **[B19]** - dictionaries: CookieInit.
- `Modules/cookie-store/CookieListItem.idl` **[B19]** - dictionaries: CookieListItem.
- `Modules/cookie-store/CookieSameSite.idl` **[B19]** - enums: CookieSameSite.
- `Modules/cookie-store/CookieStore.idl` **[B19]** - CookieStore: get, getAll, set, delete, onchange.
- `Modules/cookie-store/CookieStoreDeleteOptions.idl` **[B19]** - dictionaries: CookieStoreDeleteOptions.
- `Modules/cookie-store/CookieStoreGetOptions.idl` **[B19]** - dictionaries: CookieStoreGetOptions.
- `Modules/cookie-store/CookieStoreManager.idl` **[B19]** - CookieStoreManager: subscribe, unsubscribe, getSubscriptions.
- `Modules/cookie-store/DOMWindow+CookieStore.idl` **[B19]** - DOMWindow (partial): cookieStore.
- `Modules/cookie-store/ExtendableCookieChangeEvent.idl` **[B19]** - ExtendableCookieChangeEvent: constructor, changed, deleted.
- `Modules/cookie-store/ExtendableCookieChangeEventInit.idl` **[B19]** - dictionaries: ExtendableCookieChangeEventInit.
### Modules/credentialmanagement

- `Modules/credentialmanagement/BasicCredential.idl` **[B21]** - BasicCredential: id, type, isConditionalMediationAvailable.
- `Modules/credentialmanagement/CredentialCreationOptions.idl` **[B21]** - dictionaries: CredentialCreationOptions.
- `Modules/credentialmanagement/CredentialMediationRequirement.idl` **[B21]** - enums: CredentialMediationRequirement.
- `Modules/credentialmanagement/CredentialRequestOptions.idl` **[B21]** - dictionaries: CredentialRequestOptions.
- `Modules/credentialmanagement/CredentialsContainer.idl` **[B21]** - CredentialsContainer: get, store, create, preventSilentAccess.
- `Modules/credentialmanagement/FederatedCredentialRequestOptions.idl` **[B21]** - dictionaries: FederatedCredentialRequestOptions.
- `Modules/credentialmanagement/IdentityCredentialRequestOptions.idl` **[B21]** - dictionaries: IdentityCredentialRequestOptions.
- `Modules/credentialmanagement/Navigator+Credentials.idl` **[B21]** - Navigator (partial): credentials.
- `Modules/credentialmanagement/OTPCredentialRequestOptions.idl` **[B21]** - dictionaries: OTPCredentialRequestOptions.
### Modules/encryptedmedia

- `Modules/encryptedmedia/legacy/WebKitMediaKeyMessageEvent.idl` **[B08]** - WebKitMediaKeyMessageEvent: constructor, message, destinationURL; dictionaries: WebKitMediaKeyMessageEventInit.
- `Modules/encryptedmedia/legacy/WebKitMediaKeyNeededEvent.idl` **[B08]** - WebKitMediaKeyNeededEvent: constructor, initData; dictionaries: WebKitMediaKeyNeededEventInit.
- `Modules/encryptedmedia/legacy/WebKitMediaKeys.idl` **[B08]** - WebKitMediaKeys: constructor, createSession, isTypeSupported, keySystem.
- `Modules/encryptedmedia/legacy/WebKitMediaKeySession.idl` **[B08]** - WebKitMediaKeySession: error, keySystem, sessionId, update, close, onwebkitkeyadded, onwebkitkeyerror, onwebkitkeymessage.
- `Modules/encryptedmedia/MediaKeyEncryptionScheme.idl` **[B08]** - enums: MediaKeyEncryptionScheme.
- `Modules/encryptedmedia/MediaKeyMessageEvent.idl` **[B08]** - MediaKeyMessageEvent: constructor, messageType, message.
- `Modules/encryptedmedia/MediaKeyMessageEventInit.idl` **[B08]** - dictionaries: MediaKeyMessageEventInit.
- `Modules/encryptedmedia/MediaKeyMessageType.idl` **[B08]** - enums: MediaKeyMessageType.
- `Modules/encryptedmedia/MediaKeys.idl` **[B08]** - MediaKeys: createSession, setServerCertificate.
- `Modules/encryptedmedia/MediaKeySession.idl` **[B08]** - MediaKeySession: sessionId, expiration, closed, keyStatuses, onkeystatuseschange, onmessage, generateRequest, load, update, close, remove.
- `Modules/encryptedmedia/MediaKeySessionType.idl` **[B08]** - enums: MediaKeySessionType.
- `Modules/encryptedmedia/MediaKeysRequirement.idl` **[B08]** - enums: MediaKeysRequirement.
- `Modules/encryptedmedia/MediaKeyStatusMap.idl` **[B08]** - MediaKeyStatusMap: size, has, get; enums: MediaKeyStatus; collections: iterable<BufferSource, MediaKeyStatus>;.
- `Modules/encryptedmedia/MediaKeySystemAccess.idl` **[B08]** - MediaKeySystemAccess: keySystem, getConfiguration, createMediaKeys.
- `Modules/encryptedmedia/MediaKeySystemConfiguration.idl` **[B08]** - dictionaries: MediaKeySystemConfiguration.
- `Modules/encryptedmedia/MediaKeySystemMediaCapability.idl` **[B08]** - dictionaries: MediaKeySystemMediaCapability.
- `Modules/encryptedmedia/Navigator+EME.idl` **[B08]** - Navigator (partial): requestMediaKeySystemAccess.
### Modules/entriesapi

- `Modules/entriesapi/DOMFileSystem.idl` **[B19]** - DOMFileSystem: name, root.
- `Modules/entriesapi/ErrorCallback.idl` **[B19]** - callbacks: ErrorCallback.
- `Modules/entriesapi/FileCallback.idl` **[B19]** - callbacks: FileCallback.
- `Modules/entriesapi/FileSystemDirectoryEntry.idl` **[B19]** - FileSystemDirectoryEntry: createReader, getFile, getDirectory; dictionaries: FileSystemFlags.
- `Modules/entriesapi/FileSystemDirectoryReader.idl` **[B19]** - FileSystemDirectoryReader: readEntries.
- `Modules/entriesapi/FileSystemEntriesCallback.idl` **[B19]** - callbacks: FileSystemEntriesCallback.
- `Modules/entriesapi/FileSystemEntry.idl` **[B19]** - FileSystemEntry: isFile, isDirectory, name, fullPath, filesystem, getParent.
- `Modules/entriesapi/FileSystemEntryCallback.idl` **[B19]** - callbacks: FileSystemEntryCallback.
- `Modules/entriesapi/FileSystemFileEntry.idl` **[B19]** - FileSystemFileEntry: file.
- `Modules/entriesapi/HTMLInputElement+EntriesAPI.idl` **[B19]** - HTMLInputElement (partial): webkitdirectory, webkitEntries.
### Modules/fetch

- `Modules/fetch/FetchBody.idl` **[B18]** - FetchBody (mixin): body, bodyUsed, arrayBuffer, blob, bytes, formData, json, text.
- `Modules/fetch/FetchHeaders.idl` **[B18]** - FetchHeaders: constructor, append, delete, get, getSetCookie, has, set; collections: iterable<ByteString, ByteString>;.
- `Modules/fetch/FetchReferrerPolicy.idl` **[B18]** - enums: FetchReferrerPolicy.
- `Modules/fetch/FetchRequest.idl` **[B18]** - FetchRequest: constructor, method, url, headers, destination, referrer, referrerPolicy, mode, credentials, cache, redirect, integrity, keepalive, signal, targetAddressSpace, clone; includes: FetchRequest -> FetchBody.
- `Modules/fetch/FetchRequestCache.idl` **[B18]** - enums: FetchRequestCache.
- `Modules/fetch/FetchRequestCredentials.idl` **[B18]** - enums: FetchRequestCredentials.
- `Modules/fetch/FetchRequestDestination.idl` **[B18]** - enums: FetchRequestDestination.
- `Modules/fetch/FetchRequestInit.idl` **[B18]** - dictionaries: FetchRequestInit.
- `Modules/fetch/FetchRequestMode.idl` **[B18]** - enums: FetchRequestMode.
- `Modules/fetch/FetchRequestRedirect.idl` **[B18]** - enums: FetchRequestRedirect.
- `Modules/fetch/FetchResponse.idl` **[B18]** - FetchResponse: constructor, error, redirect, json, type, url, redirected, status, ok, statusText, headers, clone; dictionaries: FetchResponseInit; enums: FetchResponseType; includes: FetchResponse -> FetchBody.
- `Modules/fetch/IPAddressSpace.idl` **[B18]** - enums: IPAddressSpace.
- `Modules/fetch/RequestPriority.idl` **[B18]** - enums: RequestPriority.
- `Modules/fetch/WindowOrWorkerGlobalScope+Fetch.idl` **[B18]** - WindowOrWorkerGlobalScope (partial) (mixin): fetch.
### Modules/filesystem

- `Modules/filesystem/FileSystemDirectoryHandle.idl` **[B19]** - FileSystemDirectoryHandle: getFileHandle, getDirectoryHandle, removeEntry, resolve; dictionaries: FileSystemGetDirectoryOptions, FileSystemGetFileOptions, FileSystemRemoveOptions.
- `Modules/filesystem/FileSystemFileHandle.idl` **[B19]** - FileSystemFileHandle: getFile, createSyncAccessHandle, createWritable; dictionaries: FileSystemCreateWritableOptions.
- `Modules/filesystem/FileSystemHandle.idl` **[B19]** - FileSystemHandle: kind, name, isSameEntry, move; enums: FileSystemHandleKind.
- `Modules/filesystem/FileSystemSyncAccessHandle.idl` **[B19]** - FileSystemSyncAccessHandle: truncate, getSize, flush, close, read, write; dictionaries: FilesystemReadWriteOptions.
- `Modules/filesystem/FileSystemWritableFileStream.idl` **[B19]** - FileSystemWritableFileStream: write, seek, truncate; dictionaries: WriteParams; enums: WriteCommandType.
- `Modules/filesystem/StorageManager+FileSystem.idl` **[B19]** - StorageManager (partial): getDirectory.
### Modules/gamepad

- `Modules/gamepad/Gamepad.idl` **[I09]** - Gamepad: id, index, connected, timestamp, mapping, axes, buttons, vibrationActuator.
- `Modules/gamepad/GamepadButton.idl` **[I09]** - GamepadButton: pressed, value.
- `Modules/gamepad/GamepadEffectParameters.idl` **[I09]** - dictionaries: GamepadEffectParameters.
- `Modules/gamepad/GamepadEvent.idl` **[I09]** - GamepadEvent: constructor, gamepad; dictionaries: GamepadEventInit.
- `Modules/gamepad/GamepadHapticActuator.idl` **[I09]** - GamepadHapticActuator: type, canPlayEffectType, playEffect, reset, canPlay; enums: GamepadHapticActuatorType, GamepadHapticResult.
- `Modules/gamepad/GamepadHapticEffectType.idl` **[I09]** - enums: GamepadHapticEffectType.
- `Modules/gamepad/Navigator+Gamepad.idl` **[I09]** - Navigator (partial): getGamepads.
- `Modules/gamepad/WindowEventHandlers+Gamepad.idl` **[I09]** - WindowEventHandlers (partial) (mixin): ongamepadconnected, ongamepaddisconnected.
### Modules/geolocation

- `Modules/geolocation/Geolocation.idl` **[B14]** - Geolocation: getCurrentPosition, watchPosition, clearWatch.
- `Modules/geolocation/GeolocationCoordinates.idl` **[B14]** - GeolocationCoordinates: latitude, longitude, altitude, accuracy, altitudeAccuracy, heading, speed, toJSON, floorLevel.
- `Modules/geolocation/GeolocationPosition.idl` **[B14]** - GeolocationPosition: coords, timestamp, toJSON.
- `Modules/geolocation/GeolocationPositionError.idl` **[B14]** - GeolocationPositionError: code, message.
- `Modules/geolocation/Navigator+Geolocation.idl` **[B14]** - Navigator (partial): geolocation.
- `Modules/geolocation/PositionCallback.idl` **[B14]** - callbacks: PositionCallback.
- `Modules/geolocation/PositionErrorCallback.idl` **[B14]** - callbacks: PositionErrorCallback.
- `Modules/geolocation/PositionOptions.idl` **[B14]** - dictionaries: PositionOptions.
### Modules/highlight

- `Modules/highlight/Highlight.idl` **[T03]** - Highlight: constructor, priority, type; enums: HighlightType; collections: setlike<AbstractRange>;.
- `Modules/highlight/HighlightRegistry.idl` **[T03]** - HighlightRegistry: no own parsed members; collections: maplike<[AtomString] DOMString, Highlight>;.
### Modules/identity

- `Modules/identity/DigitalCredential.idl` **[B21]** - DigitalCredential: data, protocol, userAgentAllowsProtocol, toJSON.
- `Modules/identity/DigitalCredentialGetRequest.idl` **[B21]** - dictionaries: DigitalCredentialGetRequest.
- `Modules/identity/DigitalCredentialPresentationProtocol.idl` **[B21]** - enums: DigitalCredentialPresentationProtocol.
- `Modules/identity/DigitalCredentialRequestOptions.idl` **[B21]** - dictionaries: DigitalCredentialRequestOptions.
- `Modules/identity/protocols/ISO18013/MobileDocumentRequest.idl` **[B21]** - dictionaries: MobileDocumentRequest.
### Modules/indexeddb

- `Modules/indexeddb/IDBCursor.idl` **[B19]** - IDBCursor: source, direction, key, primaryKey, request, advance, continue, continuePrimaryKey, update, delete.
- `Modules/indexeddb/IDBCursorDirection.idl` **[B19]** - enums: IDBCursorDirection.
- `Modules/indexeddb/IDBCursorWithValue.idl` **[B19]** - IDBCursorWithValue: value.
- `Modules/indexeddb/IDBDatabase.idl` **[B19]** - IDBDatabase: name, version, objectStoreNames, transaction, close, createObjectStore, deleteObjectStore, onabort, onclose, onerror, onversionchange; dictionaries: IDBObjectStoreParameters, IDBTransactionOptions.
- `Modules/indexeddb/IDBFactory.idl` **[B19]** - IDBFactory: open, deleteDatabase, databases, cmp; dictionaries: IDBDatabaseInfo.
- `Modules/indexeddb/IDBGetAllOptions.idl` **[B19]** - dictionaries: IDBGetAllOptions.
- `Modules/indexeddb/IDBIndex.idl` **[B19]** - IDBIndex: name, objectStore, keyPath, multiEntry, unique, get, getKey, getAll, getAllKeys, getAllRecords, count, openCursor, openKeyCursor.
- `Modules/indexeddb/IDBKeyRange.idl` **[B19]** - IDBKeyRange: lower, upper, lowerOpen, upperOpen, only, lowerBound, upperBound, bound, includes.
- `Modules/indexeddb/IDBObjectStore.idl` **[B19]** - IDBObjectStore: name, keyPath, indexNames, transaction, autoIncrement, put, add, delete, clear, get, getKey, getAll, getAllKeys, getAllRecords, count, openCursor, openKeyCursor, index, createIndex, deleteIndex; dictionaries: IDBIndexParameters.
- `Modules/indexeddb/IDBOpenDBRequest.idl` **[B19]** - IDBOpenDBRequest: onblocked, onupgradeneeded.
- `Modules/indexeddb/IDBRecord.idl` **[B19]** - IDBRecord: key, primaryKey, value.
- `Modules/indexeddb/IDBRequest.idl` **[B19]** - IDBRequest: result, error, source, transaction, readyState, onsuccess, onerror; enums: IDBRequestReadyState.
- `Modules/indexeddb/IDBTransaction.idl` **[B19]** - IDBTransaction: objectStoreNames, mode, durability, db, error, objectStore, abort, commit, onabort, oncomplete, onerror.
- `Modules/indexeddb/IDBTransactionDurability.idl` **[B19]** - enums: IDBTransactionDurability.
- `Modules/indexeddb/IDBTransactionMode.idl` **[B19]** - enums: IDBTransactionMode.
- `Modules/indexeddb/IDBVersionChangeEvent.idl` **[B19]** - IDBVersionChangeEvent: constructor, oldVersion, newVersion; dictionaries: IDBVersionChangeEventInit.
- `Modules/indexeddb/WindowOrWorkerGlobalScope+IndexedDatabase.idl` **[B19]** - WindowOrWorkerGlobalScope (partial) (mixin): indexedDB, webkitIndexedDB.
### Modules/mediacapabilities

- `Modules/mediacapabilities/AudioConfiguration.idl` **[B06]** - dictionaries: AudioConfiguration.
- `Modules/mediacapabilities/ColorGamut.idl` **[B06]** - enums: ColorGamut.
- `Modules/mediacapabilities/HdrMetadataType.idl` **[B06]** - enums: HdrMetadataType.
- `Modules/mediacapabilities/MediaCapabilities.idl` **[B06]** - MediaCapabilities: decodingInfo, encodingInfo.
- `Modules/mediacapabilities/MediaCapabilitiesDecodingInfo.idl` **[B06]** - dictionaries: MediaCapabilitiesDecodingInfo.
- `Modules/mediacapabilities/MediaCapabilitiesEncodingInfo.idl` **[B06]** - dictionaries: MediaCapabilitiesEncodingInfo.
- `Modules/mediacapabilities/MediaCapabilitiesInfo.idl` **[B06]** - dictionaries: MediaCapabilitiesInfo.
- `Modules/mediacapabilities/MediaConfiguration.idl` **[B06]** - dictionaries: MediaConfiguration.
- `Modules/mediacapabilities/MediaDecodingConfiguration.idl` **[B06]** - dictionaries: MediaDecodingConfiguration.
- `Modules/mediacapabilities/MediaDecodingType.idl` **[B06]** - enums: MediaDecodingType.
- `Modules/mediacapabilities/MediaEncodingConfiguration.idl` **[B06]** - dictionaries: MediaEncodingConfiguration.
- `Modules/mediacapabilities/MediaEncodingType.idl` **[B06]** - enums: MediaEncodingType.
- `Modules/mediacapabilities/Navigator+MediaCapabilities.idl` **[B06]** - Navigator (partial): mediaCapabilities.
- `Modules/mediacapabilities/TransferFunction.idl` **[B06]** - enums: TransferFunction.
- `Modules/mediacapabilities/VideoConfiguration.idl` **[B06]** - dictionaries: VideoConfiguration.
- `Modules/mediacapabilities/WorkerNavigator+MediaCapabilities.idl` **[B06]** - WorkerNavigator (partial): mediaCapabilities.
### Modules/mediacontrols

- `Modules/mediacontrols/DOMWindow+MediaControls.idl` **[B02]** - DOMWindow (partial): utils.
- `Modules/mediacontrols/MediaControlsContextMenuItem.idl` **[B02]** - dictionaries: MediaControlsContextMenuItem.
- `Modules/mediacontrols/MediaControlsHost.idl` **[B02]** - MediaControlsHost: layoutTraitsClassName, mediaControlsContainerClassName, brightness, sortedTrackListForMenu, displayNameForTrack, captionMenuOffItem, captionMenuAutomaticItem, captionMenuOnItem, captionDisplayMode, setSelectedTextTrack, textTrackContainer, allowsInlineMediaPlayback, supportsFullscreen, isVideoLayerInline, userGestureRequired, isInMediaDocument, shouldForceControlsDisplay, supportsSeeking, inWindowFullscreen, supportsRewind, needsChromeMediaControlsPseudoElement, isMediaControlsMacInlineSizeSpecsEnabled, isAVExperienceControllerFullscreenEnabled, externalDeviceDisplayName, externalDeviceRouteName, externalDeviceType, controlsDependOnPageScaleFactor, updateTextTrackContainer, enteredFullscreen, exitedFullscreen, presentationModeChanged, generateUUID, sourceType, shadowRootStyleSheets, base64StringForIconNameAndType, showMediaControlsContextMenu, mediaControlsContextMenuItems, ensureMediaSessionObserver, controller; enums: DeviceType, SourceType.
- `Modules/mediacontrols/MediaControlsUtils.idl` **[B02]** - MediaControlsUtils: formattedStringForDuration, createImageForIconNameAndType.
### Modules/mediarecorder

- `Modules/mediarecorder/BlobEvent.idl` **[B04]** - BlobEvent: constructor, data, timecode; dictionaries: BlobEventInit.
- `Modules/mediarecorder/MediaRecorder.idl` **[B04]** - MediaRecorder: constructor, stream, mimeType, state, onstart, onstop, ondataavailable, onpause, onresume, onerror, videoBitsPerSecond, audioBitsPerSecond, start, stop, pause, resume, requestData, isTypeSupported; dictionaries: MediaRecorderOptions; enums: RecordingState.
- `Modules/mediarecorder/MediaRecorderErrorEvent.idl` **[B04]** - MediaRecorderErrorEvent: constructor, error; dictionaries: MediaRecorderErrorEventInit.
### Modules/mediasession

- `Modules/mediasession/MediaImage.idl` **[B07]** - dictionaries: MediaImage.
- `Modules/mediasession/MediaMetadata.idl` **[B07]** - MediaMetadata: constructor, title, artist, album, artwork; includes: MediaMetadata -> MediaMetadataPlaylistMixin.
- `Modules/mediasession/MediaMetadataInit.idl` **[B07]** - dictionaries: MediaMetadataInit.
- `Modules/mediasession/MediaMetadataPlaylistMixin.idl` **[B07]** - MediaMetadataPlaylistMixin (mixin): trackIdentifier.
- `Modules/mediasession/MediaPositionState.idl` **[B07]** - dictionaries: MediaPositionState.
- `Modules/mediasession/MediaSession.idl` **[B07]** - MediaSession: metadata, playbackState, setActionHandler, callActionHandler, setPositionState, setMicrophoneActive, setCameraActive, setScreenshareActive, captionTracks, captionsEnabled; includes: MediaSession -> MediaSessionCoordinatorMixin, MediaSession -> MediaSessionPlaylistMixin.
- `Modules/mediasession/MediaSessionAction.idl` **[B07]** - enums: MediaSessionAction.
- `Modules/mediasession/MediaSessionActionDetails.idl` **[B07]** - dictionaries: MediaSessionActionDetails.
- `Modules/mediasession/MediaSessionActionHandler.idl` **[B07]** - callbacks: MediaSessionActionHandler.
- `Modules/mediasession/MediaSessionCaptionTrack.idl` **[B07]** - dictionaries: MediaSessionCaptionTrack.
- `Modules/mediasession/MediaSessionCoordinator.idl` **[B07]** - MediaSessionCoordinator: join, leave, state, oncoordinatorstatechange, identifier, seekTo, play, pause, setTrack.
- `Modules/mediasession/MediaSessionCoordinatorMixin.idl` **[B07]** - MediaSessionCoordinatorMixin (mixin): coordinator, readyState.
- `Modules/mediasession/MediaSessionCoordinatorState.idl` **[B07]** - enums: MediaSessionCoordinatorState.
- `Modules/mediasession/MediaSessionPlaybackState.idl` **[B07]** - enums: MediaSessionPlaybackState.
- `Modules/mediasession/MediaSessionPlaylistMixin.idl` **[B07]** - MediaSessionPlaylistMixin (mixin): playlist.
- `Modules/mediasession/MediaSessionReadyState.idl` **[B07]** - enums: MediaSessionReadyState.
- `Modules/mediasession/Navigator+MediaSession.idl` **[B07]** - Navigator (partial): mediaSession.
### Modules/mediasource

- `Modules/mediasource/AudioTrack+MediaSource.idl` **[B05]** - AudioTrack (partial): sourceBuffer.
- `Modules/mediasource/BufferedChangeEvent.idl` **[B05]** - BufferedChangeEvent: constructor, addedRanges, removedRanges; dictionaries: BufferedChangeEventInit.
- `Modules/mediasource/DOMURL+MediaSource.idl` **[B05]** - DOMURL (partial): createObjectURL.
- `Modules/mediasource/ManagedMediaSource.idl` **[B05]** - ManagedMediaSource: constructor, quality, onqualitychange, streaming, onstartstreaming, onendstreaming, isTypeSupported; enums: PreferredQuality.
- `Modules/mediasource/ManagedSourceBuffer.idl` **[B05]** - ManagedSourceBuffer: onbufferedchange.
- `Modules/mediasource/MediaSource.idl` **[B05]** - MediaSource: constructor, detachable, handle, sourceBuffers, activeSourceBuffers, readyState, duration, onsourceopen, onsourceended, onsourceclose, canConstructInDedicatedWorker, addSourceBuffer, removeSourceBuffer, endOfStream, setLiveSeekableRange, clearLiveSeekableRange, isTypeSupported; enums: EndOfStreamError, ReadyState.
- `Modules/mediasource/MediaSourceHandle.idl` **[B05]** - MediaSourceHandle: no own parsed members.
- `Modules/mediasource/MediaSourceInit.idl` **[B05]** - dictionaries: MediaSourceInit.
- `Modules/mediasource/SourceBuffer.idl` **[B05]** - SourceBuffer: mode, updating, buffered, timestampOffset, audioTracks, videoTracks, textTracks, appendWindowStart, appendWindowEnd, appendBuffer, abort, remove, onupdatestart, onupdate, onupdateend, onerror, onabort, changeType; enums: AppendMode.
- `Modules/mediasource/SourceBufferList.idl` **[B05]** - SourceBufferList: length, item, onaddsourcebuffer, onremovesourcebuffer.
- `Modules/mediasource/TextTrack+MediaSource.idl` **[B05]** - TextTrack (partial): sourceBuffer.
- `Modules/mediasource/VideoPlaybackQuality.idl` **[B05]** - VideoPlaybackQuality: creationTime, droppedVideoFrames, totalVideoFrames, corruptedVideoFrames, displayCompositedVideoFrames, totalFrameDelay.
- `Modules/mediasource/VideoTrack+MediaSource.idl` **[B05]** - VideoTrack (partial): sourceBuffer.
### Modules/mediastream

- `Modules/mediastream/CanvasCaptureMediaStreamTrack.idl` **[B04, B18]** - CanvasCaptureMediaStreamTrack: canvas, requestFrame.
- `Modules/mediastream/DoubleRange.idl` **[B04, B18]** - dictionaries: DoubleRange.
- `Modules/mediastream/FillLightMode.idl` **[B04, B18]** - enums: FillLightMode.
- `Modules/mediastream/ImageCapture.idl` **[B04, B18]** - ImageCapture: constructor, getPhotoCapabilities, takePhoto, grabFrame, getPhotoSettings, track.
- `Modules/mediastream/InputDeviceInfo.idl` **[B04, B18]** - InputDeviceInfo: getCapabilities.
- `Modules/mediastream/LongRange.idl` **[B04, B18]** - dictionaries: LongRange.
- `Modules/mediastream/MediaDeviceInfo.idl` **[B04, B18]** - MediaDeviceInfo: deviceId, kind, label, groupId, toJSON; enums: MediaDeviceKind.
- `Modules/mediastream/MediaDevices.idl` **[B04, B18]** - MediaDevices: ondevicechange, enumerateDevices, getSupportedConstraints, getUserMedia, getDisplayMedia; dictionaries: DisplayMediaStreamConstraints, MediaStreamConstraints; enums: DisplayCaptureSurfaceType.
- `Modules/mediastream/MediaSettingsRange.idl` **[B04, B18]** - dictionaries: MediaSettingsRange.
- `Modules/mediastream/MediaStream.idl` **[B04, B18]** - MediaStream: constructor, id, getAudioTracks, getVideoTracks, getTracks, getTrackById, addTrack, removeTrack, clone, active, onaddtrack, onremovetrack.
- `Modules/mediastream/MediaStreamTrack.idl` **[B04, B18]** - MediaStreamTrack: kind, id, label, enabled, contentHint, muted, onmute, onunmute, onconfigurationchange, readyState, onended, clone, stop, getCapabilities, getConstraints, getSettings, applyConstraints; dictionaries: MediaTrackSettings; enums: MediaStreamTrackState.
- `Modules/mediastream/MediaStreamTrackEvent.idl` **[B04, B18]** - MediaStreamTrackEvent: constructor, track; dictionaries: MediaStreamTrackEventInit.
- `Modules/mediastream/MediaStreamTrackHandle.idl` **[B04, B18]** - MediaStreamTrackHandle: constructor.
- `Modules/mediastream/MediaStreamTrackProcessor.idl` **[B04, B18]** - MediaStreamTrackProcessor: constructor, readable; dictionaries: MediaStreamTrackProcessorInit.
- `Modules/mediastream/MediaTrackCapabilities.idl` **[B04, B18]** - dictionaries: MediaTrackCapabilities.
- `Modules/mediastream/MediaTrackConstraints.idl` **[B04, B18]** - dictionaries: ConstrainBooleanParameters, ConstrainDOMStringParameters, ConstrainDoubleRange, ConstrainLongRange, MediaTrackConstraintSet, MediaTrackConstraints.
- `Modules/mediastream/MediaTrackSupportedConstraints.idl` **[B04, B18]** - dictionaries: MediaTrackSupportedConstraints.
- `Modules/mediastream/MeteringMode.idl` **[B04, B18]** - enums: MeteringMode.
- `Modules/mediastream/Navigator+MediaDevices.idl` **[B04, B18]** - Navigator (partial): mediaDevices, getUserMedia.
- `Modules/mediastream/OverconstrainedError.idl` **[B04, B18]** - OverconstrainedError: constructor, constraint.
- `Modules/mediastream/OverconstrainedErrorEvent.idl` **[B04, B18]** - OverconstrainedErrorEvent: constructor, error; dictionaries: OverconstrainedErrorEventInit.
- `Modules/mediastream/PhotoCapabilities.idl` **[B04, B18]** - dictionaries: PhotoCapabilities.
- `Modules/mediastream/PhotoSettings.idl` **[B04, B18]** - dictionaries: PhotoSettings.
- `Modules/mediastream/RedEyeReduction.idl` **[B04, B18]** - enums: RedEyeReduction.
- `Modules/mediastream/RTCAnswerOptions.idl` **[B04, B18]** - dictionaries: RTCAnswerOptions.
- `Modules/mediastream/RTCCertificate.idl` **[B04, B18]** - RTCCertificate: expires, getFingerprints; dictionaries: RTCDtlsFingerprint.
- `Modules/mediastream/RTCConfiguration.idl` **[B04, B18]** - dictionaries: RTCConfiguration; enums: RTCBundlePolicy, RTCConfigurationTargetLatency, RTCIceTransportPolicy, RTCPMuxPolicy.
- `Modules/mediastream/RTCDataChannel.idl` **[B04, B18]** - RTCDataChannel: label, ordered, maxPacketLifeTime, maxRetransmits, protocol, negotiated, id, readyState, bufferedAmount, priority, bufferedAmountLowThreshold, binaryType, send, close, onopen, onerror, onclose, onclosing, onmessage, onbufferedamountlow; enums: BinaryType, RTCDataChannelState.
- `Modules/mediastream/RTCDataChannelEvent.idl` **[B04, B18]** - RTCDataChannelEvent: constructor, channel; dictionaries: RTCDataChannelEventInit.
- `Modules/mediastream/RTCDegradationPreference.idl` **[B04, B18]** - enums: RTCDegradationPreference.
- `Modules/mediastream/RTCDtlsTransport.idl` **[B04, B18]** - RTCDtlsTransport: iceTransport, state, getRemoteCertificates, onstatechange, onerror.
- `Modules/mediastream/RTCDtlsTransportState.idl` **[B04, B18]** - enums: RTCDtlsTransportState.
- `Modules/mediastream/RTCDTMFSender.idl` **[B04, B18]** - RTCDTMFSender: insertDTMF, ontonechange, canInsertDTMF, toneBuffer.
- `Modules/mediastream/RTCDTMFToneChangeEvent.idl` **[B04, B18]** - RTCDTMFToneChangeEvent: constructor, tone; dictionaries: RTCDTMFToneChangeEventInit.
- `Modules/mediastream/RTCEncodedAudioFrame.idl` **[B04, B18]** - RTCEncodedAudioFrame: constructor, timestamp, data, getMetadata; dictionaries: RTCEncodedAudioFrameMetadata, RTCEncodedAudioFrameOptions.
- `Modules/mediastream/RTCEncodedStreams.idl` **[B04, B18]** - dictionaries: RTCEncodedStreams.
- `Modules/mediastream/RTCEncodedVideoFrame.idl` **[B04, B18]** - RTCEncodedVideoFrame: constructor, type, timestamp, data, getMetadata; dictionaries: RTCEncodedVideoFrameMetadata, RTCEncodedVideoFrameOptions; enums: RTCEncodedVideoFrameType.
- `Modules/mediastream/RTCError.idl` **[B04, B18]** - RTCError: constructor, errorDetail, sdpLineNumber, sctpCauseCode, receivedAlert, sentAlert; dictionaries: RTCErrorInit.
- `Modules/mediastream/RTCErrorDetailType.idl` **[B04, B18]** - enums: RTCErrorDetailType.
- `Modules/mediastream/RTCErrorEvent.idl` **[B04, B18]** - RTCErrorEvent: constructor, error; dictionaries: RTCErrorEventInit.
- `Modules/mediastream/RTCIceCandidate.idl` **[B04, B18]** - RTCIceCandidate: constructor, candidate, sdpMid, sdpMLineIndex, foundation, component, priority, address, protocol, port, type, tcpType, relatedAddress, relatedPort, usernameFragment, toJSON.
- `Modules/mediastream/RTCIceCandidateInit.idl` **[B04, B18]** - dictionaries: RTCIceCandidateInit.
- `Modules/mediastream/RTCIceCandidateType.idl` **[B04, B18]** - enums: RTCIceCandidateType.
- `Modules/mediastream/RTCIceComponent.idl` **[B04, B18]** - enums: RTCIceComponent.
- `Modules/mediastream/RTCIceConnectionState.idl` **[B04, B18]** - enums: RTCIceConnectionState.
- `Modules/mediastream/RTCIceGatheringState.idl` **[B04, B18]** - enums: RTCIceGatheringState.
- `Modules/mediastream/RTCIceProtocol.idl` **[B04, B18]** - enums: RTCIceProtocol.
- `Modules/mediastream/RTCIceRole.idl` **[B04, B18]** - enums: RTCIceRole.
- `Modules/mediastream/RTCIceServer.idl` **[B04, B18]** - dictionaries: RTCIceServer.
- `Modules/mediastream/RTCIceServerTransportProtocol.idl` **[B04, B18]** - enums: RTCIceServerTransportProtocol.
- `Modules/mediastream/RTCIceTcpCandidateType.idl` **[B04, B18]** - enums: RTCIceTcpCandidateType.
- `Modules/mediastream/RTCIceTransport.idl` **[B04, B18]** - RTCIceTransport: state, gatheringState, getSelectedCandidatePair, onstatechange, ongatheringstatechange, onselectedcandidatepairchange; dictionaries: RTCIceCandidatePair.
- `Modules/mediastream/RTCIceTransportState.idl` **[B04, B18]** - enums: RTCIceTransportState.
- `Modules/mediastream/RTCLocalSessionDescriptionInit.idl` **[B04, B18]** - dictionaries: RTCLocalSessionDescriptionInit.
- `Modules/mediastream/RTCOfferAnswerOptions.idl` **[B04, B18]** - dictionaries: RTCOfferAnswerOptions.
- `Modules/mediastream/RTCOfferOptions.idl` **[B04, B18]** - dictionaries: RTCOfferOptions.
- `Modules/mediastream/RTCPeerConnection.idl` **[B04, B18]** - RTCPeerConnection: constructor, createOffer, createAnswer, setLocalDescription, localDescription, currentLocalDescription, pendingLocalDescription, setRemoteDescription, remoteDescription, currentRemoteDescription, pendingRemoteDescription, addIceCandidate, signalingState, iceGatheringState, iceConnectionState, connectionState, canTrickleIceCandidates, restartIce, getConfiguration, setConfiguration, close, onnegotiationneeded, onicecandidate, onsignalingstatechange, oniceconnectionstatechange, onicegatheringstatechange, onconnectionstatechange, onicecandidateerror, generateCertificate, getSenders, getReceivers, getTransceivers, addTrack, removeTrack, addTransceiver, ontrack, sctp, createDataChannel, ondatachannel, getStats; dictionaries: RTCCertificateParameters, RTCDataChannelInit, RTCRtpTransceiverInit.
- `Modules/mediastream/RTCPeerConnectionIceErrorEvent.idl` **[B04, B18]** - RTCPeerConnectionIceErrorEvent: constructor, address, port, url, errorCode, errorText; dictionaries: RTCPeerConnectionIceErrorEventInit.
- `Modules/mediastream/RTCPeerConnectionIceEvent.idl` **[B04, B18]** - RTCPeerConnectionIceEvent: constructor, candidate, url; dictionaries: RTCPeerConnectionIceEventInit.
- `Modules/mediastream/RTCPeerConnectionState.idl` **[B04, B18]** - enums: RTCPeerConnectionState.
- `Modules/mediastream/RTCPriorityType.idl` **[B04, B18]** - enums: RTCPriorityType.
- `Modules/mediastream/RTCRtcpParameters.idl` **[B04, B18]** - dictionaries: RTCRtcpParameters.
- `Modules/mediastream/RTCRtpCapabilities.idl` **[B04, B18]** - dictionaries: RTCRtpCapabilities, RTCRtpHeaderExtensionCapability.
- `Modules/mediastream/RTCRtpCodec.idl` **[B04, B18]** - dictionaries: RTCRtpCodec.
- `Modules/mediastream/RTCRtpCodecParameters.idl` **[B04, B18]** - dictionaries: RTCRtpCodecParameters.
- `Modules/mediastream/RTCRtpCodingParameters.idl` **[B04, B18]** - dictionaries: RTCRtpCodingParameters.
- `Modules/mediastream/RTCRtpContributingSource.idl` **[B04, B18]** - dictionaries: RTCRtpContributingSource.
- `Modules/mediastream/RTCRtpDecodingParameters.idl` **[B04, B18]** - dictionaries: RTCRtpDecodingParameters.
- `Modules/mediastream/RTCRtpEncodingParameters.idl` **[B04, B18]** - dictionaries: RTCRtpEncodingParameters.
- `Modules/mediastream/RTCRtpHeaderExtensionParameters.idl` **[B04, B18]** - dictionaries: RTCRtpHeaderExtensionParameters.
- `Modules/mediastream/RTCRtpParameters.idl` **[B04, B18]** - dictionaries: RTCRtpParameters.
- `Modules/mediastream/RTCRtpReceiver+Transform.idl` **[B04, B18]** - RTCRtpReceiver (partial): transform.
- `Modules/mediastream/RTCRtpReceiver.idl` **[B04, B18]** - RTCRtpReceiver: track, transport, getCapabilities, getParameters, getContributingSources, getSynchronizationSources, getStats, jitterBufferTarget, createEncodedStreams.
- `Modules/mediastream/RTCRtpScriptTransform.idl` **[B04, B18]** - RTCRtpScriptTransform: constructor.
- `Modules/mediastream/RTCRtpScriptTransformer.idl` **[B04, B18]** - RTCRtpScriptTransformer: readable, writable, options, generateKeyFrame, sendKeyFrameRequest.
- `Modules/mediastream/RTCRtpScriptTransformProvider.idl` **[B04, B18]** - RTCRtpScriptTransformProvider (mixin): onrtctransform.
- `Modules/mediastream/RTCRtpSender+Transform.idl` **[B04, B18]** - RTCRtpSender (partial): transform.
- `Modules/mediastream/RTCRtpSender.idl` **[B04, B18]** - RTCRtpSender: track, transport, getCapabilities, getParameters, setParameters, replaceTrack, setStreams, getStats, dtmf, createEncodedStreams.
- `Modules/mediastream/RTCRtpSendParameters.idl` **[B04, B18]** - dictionaries: RTCRtpSendParameters.
- `Modules/mediastream/RTCRtpSFrameTransformErrorEvent.idl` **[B04, B18]** - RTCRtpSFrameTransformErrorEvent: constructor, errorType; dictionaries: RTCRtpSFrameTransformErrorEventInit; enums: RTCRtpSFrameTransformErrorEventType.
- `Modules/mediastream/RTCRtpSynchronizationSource.idl` **[B04, B18]** - dictionaries: RTCRtpSynchronizationSource.
- `Modules/mediastream/RTCRtpTransceiver.idl` **[B04, B18]** - RTCRtpTransceiver: mid, sender, receiver, stopped, direction, currentDirection, stop, setCodecPreferences.
- `Modules/mediastream/RTCRtpTransceiverDirection.idl` **[B04, B18]** - enums: RTCRtpTransceiverDirection.
- `Modules/mediastream/RTCSctpTransport.idl` **[B04, B18]** - RTCSctpTransport: transport, state, maxMessageSize, maxChannels, onstatechange.
- `Modules/mediastream/RTCSctpTransportState.idl` **[B04, B18]** - enums: RTCSctpTransportState.
- `Modules/mediastream/RTCSdpType.idl` **[B04, B18]** - enums: RTCSdpType.
- `Modules/mediastream/RTCSessionDescription.idl` **[B04, B18]** - RTCSessionDescription: constructor, type, sdp, toJSON.
- `Modules/mediastream/RTCSessionDescriptionInit.idl` **[B04, B18]** - dictionaries: RTCSessionDescriptionInit.
- `Modules/mediastream/RTCSignalingState.idl` **[B04, B18]** - enums: RTCSignalingState.
- `Modules/mediastream/RTCStatsReport.idl` **[B04, B18]** - RTCStatsReport: no own parsed members; dictionaries: RTCAudioPlayoutStats, RTCAudioSourceStats, RTCCertificateStats, RTCCodecStats, RTCDataChannelStats, RTCIceCandidatePairStats, RTCIceCandidateStats, RTCInboundRtpStreamStats, RTCMediaSourceStats, RTCOutboundRtpStreamStats, RTCPeerConnectionStats, RTCReceivedRtpStreamStats, RTCRemoteInboundRtpStreamStats, RTCRemoteOutboundRtpStreamStats, RTCRtpStreamStats, RTCSentRtpStreamStats, RTCStats, RTCTransportStats, RTCVideoSourceStats; enums: RTCCodecType, RTCDtlsRole, RTCQualityLimitationReason, RTCStatsIceCandidatePairState, RTCStatsType; collections: readonly maplike<DOMString, object>;.
- `Modules/mediastream/RTCTrackEvent.idl` **[B04, B18]** - RTCTrackEvent: constructor, receiver, track, streams, transceiver; dictionaries: RTCTrackEventInit.
- `Modules/mediastream/RTCTransformEvent.idl` **[B04, B18]** - RTCTransformEvent: constructor, transformer.
- `Modules/mediastream/VideoTrackGenerator.idl` **[B04, B18]** - VideoTrackGenerator: constructor, writable, muted, track.
### Modules/model-element

- `Modules/model-element/HTMLModelElement.idl` **[R16]** - HTMLModelElement: width, height, src, currentSrc, complete, ready, interactive, boundingBoxCenter, boundingBoxExtents, entityTransform, autoplay, loop, playbackRate, duration, paused, play, pause, currentTime, stageMode, environmentMap, environmentMapReady, noportal, enterFullscreen, onimmersivechange, onimmersiveerror, requestImmersive.
- `Modules/model-element/HTMLModelElementCamera.idl` **[R16]** - dictionaries: HTMLModelElementCamera.
### Modules/notifications

- `Modules/notifications/Notification.idl` **[B09]** - Notification: constructor, permission, requestPermission, onclick, onerror, navigate, title, dir, lang, body, tag, icon, silent, data, close, show, onclose, ondisplay, onshow.
- `Modules/notifications/NotificationDirection.idl` **[B09]** - enums: NotificationDirection.
- `Modules/notifications/NotificationEvent.idl` **[B09]** - NotificationEvent: constructor, notification, action; dictionaries: NotificationEventInit.
- `Modules/notifications/NotificationOptions.idl` **[B09]** - dictionaries: NotificationOptions.
- `Modules/notifications/NotificationPermission.idl` **[B09]** - enums: NotificationPermission.
- `Modules/notifications/NotificationPermissionCallback.idl` **[B09]** - callbacks: NotificationPermissionCallback.
### Modules/paymentrequest

- `Modules/paymentrequest/AddressErrors.idl` **[B21]** - dictionaries: AddressErrors.
- `Modules/paymentrequest/MerchantValidationEvent.idl` **[B21]** - MerchantValidationEvent: constructor, methodName, validationURL, complete; dictionaries: MerchantValidationEventInit.
- `Modules/paymentrequest/PayerErrorFields.idl` **[B21]** - dictionaries: PayerErrorFields.
- `Modules/paymentrequest/PaymentAddress.idl` **[B21]** - PaymentAddress: toJSON, country, addressLine, region, city, dependentLocality, postalCode, sortingCode, organization, recipient, phone.
- `Modules/paymentrequest/PaymentComplete.idl` **[B21]** - enums: PaymentComplete.
- `Modules/paymentrequest/PaymentCompleteDetails.idl` **[B21]** - dictionaries: PaymentCompleteDetails.
- `Modules/paymentrequest/PaymentCurrencyAmount.idl` **[B21]** - dictionaries: PaymentCurrencyAmount.
- `Modules/paymentrequest/PaymentDetailsBase.idl` **[B21]** - dictionaries: PaymentDetailsBase.
- `Modules/paymentrequest/PaymentDetailsInit.idl` **[B21]** - dictionaries: PaymentDetailsInit.
- `Modules/paymentrequest/PaymentDetailsModifier.idl` **[B21]** - dictionaries: PaymentDetailsModifier.
- `Modules/paymentrequest/PaymentDetailsUpdate.idl` **[B21]** - dictionaries: PaymentDetailsUpdate.
- `Modules/paymentrequest/PaymentItem.idl` **[B21]** - dictionaries: PaymentItem.
- `Modules/paymentrequest/PaymentMethodChangeEvent.idl` **[B21]** - PaymentMethodChangeEvent: constructor, methodName, methodDetails; dictionaries: PaymentMethodChangeEventInit.
- `Modules/paymentrequest/PaymentMethodData.idl` **[B21]** - dictionaries: PaymentMethodData.
- `Modules/paymentrequest/PaymentOptions.idl` **[B21]** - dictionaries: PaymentOptions.
- `Modules/paymentrequest/PaymentRequest.idl` **[B21]** - PaymentRequest: constructor, show, abort, canMakePayment, id, shippingAddress, shippingOption, shippingType, onmerchantvalidation, onshippingaddresschange, onshippingoptionchange, onpaymentmethodchange.
- `Modules/paymentrequest/PaymentRequestUpdateEvent.idl` **[B21]** - PaymentRequestUpdateEvent: constructor, updateWith.
- `Modules/paymentrequest/PaymentRequestUpdateEventInit.idl` **[B21]** - dictionaries: PaymentRequestUpdateEventInit.
- `Modules/paymentrequest/PaymentResponse.idl` **[B21]** - PaymentResponse: toJSON, requestId, methodName, details, shippingAddress, shippingOption, payerName, payerEmail, payerPhone, complete, retry, onpayerdetailchange.
- `Modules/paymentrequest/PaymentShippingOption.idl` **[B21]** - dictionaries: PaymentShippingOption.
- `Modules/paymentrequest/PaymentShippingType.idl` **[B21]** - enums: PaymentShippingType.
- `Modules/paymentrequest/PaymentValidationErrors.idl` **[B21]** - dictionaries: PaymentValidationErrors.
### Modules/permissions

- `Modules/permissions/Navigator+Permissions.idl` **[B21]** - Navigator (partial): permissions.
- `Modules/permissions/PermissionDescriptor.idl` **[B21]** - dictionaries: PermissionDescriptor.
- `Modules/permissions/PermissionName.idl` **[B21]** - enums: PermissionName.
- `Modules/permissions/Permissions.idl` **[B21]** - Permissions: query.
- `Modules/permissions/PermissionState.idl` **[B21]** - enums: PermissionState.
- `Modules/permissions/PermissionStatus.idl` **[B21]** - PermissionStatus: state, name, onchange.
- `Modules/permissions/WorkerNavigator+Permissions.idl` **[B21]** - WorkerNavigator (partial): permissions.
### Modules/pictureinpicture

- `Modules/pictureinpicture/Document+PictureInPicture.idl` **[B07]** - Document (partial): pictureInPictureEnabled, exitPictureInPicture.
- `Modules/pictureinpicture/DocumentOrShadowRoot+PictureInPicture.idl` **[B07]** - DocumentOrShadowRoot (partial) (mixin): pictureInPictureElement.
- `Modules/pictureinpicture/HTMLVideoElement+PictureInPicture.idl` **[B07]** - HTMLVideoElement (partial): requestPictureInPicture, onenterpictureinpicture, onleavepictureinpicture, autoPictureInPicture, disablePictureInPicture.
- `Modules/pictureinpicture/PictureInPictureEvent.idl` **[B07]** - PictureInPictureEvent: constructor, pictureInPictureWindow; dictionaries: PictureInPictureEventInit.
- `Modules/pictureinpicture/PictureInPictureWindow.idl` **[B07]** - PictureInPictureWindow: width, height, onresize.
### Modules/push-api

- `Modules/push-api/PushEncryptionKeyName.idl` **[B25]** - enums: PushEncryptionKeyName.
- `Modules/push-api/PushEvent.idl` **[B25]** - PushEvent: constructor, data, notification, appBadge.
- `Modules/push-api/PushEventInit.idl` **[B25]** - dictionaries: PushEventInit.
- `Modules/push-api/PushManager.idl` **[B25]** - PushManager: supportedContentEncodings, subscribe, getSubscription, permissionState.
- `Modules/push-api/PushMessageData.idl` **[B25]** - PushMessageData: arrayBuffer, blob, bytes, json, text.
- `Modules/push-api/PushPermissionState.idl` **[B25]** - enums: PushPermissionState.
- `Modules/push-api/PushSubscription.idl` **[B25]** - PushSubscription: endpoint, expirationTime, options, getKey, unsubscribe, toJSON.
- `Modules/push-api/PushSubscriptionChangeEvent.idl` **[B25]** - PushSubscriptionChangeEvent: constructor, newSubscription, oldSubscription.
- `Modules/push-api/PushSubscriptionChangeEventInit.idl` **[B25]** - dictionaries: PushSubscriptionChangeEventInit.
- `Modules/push-api/PushSubscriptionJSON.idl` **[B25]** - dictionaries: PushSubscriptionJSON.
- `Modules/push-api/PushSubscriptionOptions.idl` **[B25]** - PushSubscriptionOptions: userVisibleOnly, applicationServerKey.
- `Modules/push-api/PushSubscriptionOptionsInit.idl` **[B25]** - dictionaries: PushSubscriptionOptionsInit.
- `Modules/push-api/ServiceWorkerGlobalScope+PushAPI.idl` **[B25]** - ServiceWorkerGlobalScope (partial): onpush, onpushsubscriptionchange.
- `Modules/push-api/ServiceWorkerRegistration+PushAPI.idl` **[B25]** - ServiceWorkerRegistration (partial): pushManager.
- `Modules/push-api/WindowPush.idl` **[B25]** - WindowPush (mixin): pushManager.
### Modules/remoteplayback

- `Modules/remoteplayback/HTMLMediaElement+RemotePlayback.idl` **[B07]** - HTMLMediaElement (partial): remote, disableRemotePlayback.
- `Modules/remoteplayback/RemotePlayback.idl` **[B07]** - RemotePlayback: watchAvailability, cancelWatchAvailability, state, onconnecting, onconnect, ondisconnect, prompt; enums: RemotePlaybackState.
- `Modules/remoteplayback/RemotePlaybackAvailabilityCallback.idl` **[B07]** - callbacks: RemotePlaybackAvailabilityCallback.
### Modules/reporting

- `Modules/reporting/DeprecationReportBody.idl` **[H10, B25]** - DeprecationReportBody: id, anticipatedRemoval, message, sourceFile, lineNumber, columnNumber, toJSON.
- `Modules/reporting/IntegrityPolicyViolationReportBody.idl` **[H10, B25]** - IntegrityPolicyViolationReportBody: documentURL, blockedURL, destination, reportOnly, toJSON.
- `Modules/reporting/Report.idl` **[H10, B25]** - Report: toJSON, type, url, body.
- `Modules/reporting/ReportBody.idl` **[H10, B25]** - ReportBody: toJSON.
- `Modules/reporting/ReportingObserver.idl` **[H10, B25]** - ReportingObserver: constructor, observe, disconnect, takeRecords; dictionaries: ReportingObserverOptions.
- `Modules/reporting/ReportingObserverCallback.idl` **[H10, B25]** - callbacks: ReportingObserverCallback.
- `Modules/reporting/TestReportBody.idl` **[H10, B25]** - TestReportBody: message, toJSON.
### Modules/screen-wake-lock

- `Modules/screen-wake-lock/Navigator+ScreenWakeLock.idl` **[B10]** - Navigator (partial): wakeLock.
- `Modules/screen-wake-lock/WakeLock.idl` **[B10]** - WakeLock: request.
- `Modules/screen-wake-lock/WakeLockSentinel.idl` **[B10]** - WakeLockSentinel: released, type, release, onrelease.
- `Modules/screen-wake-lock/WakeLockType.idl` **[B10]** - enums: WakeLockType.
### Modules/ShapeDetection

- `Modules/ShapeDetection/BarcodeDetector.idl` **[R15]** - BarcodeDetector: constructor, getSupportedFormats, detect.
- `Modules/ShapeDetection/BarcodeDetectorOptions.idl` **[R15]** - dictionaries: BarcodeDetectorOptions.
- `Modules/ShapeDetection/BarcodeFormat.idl` **[R15]** - enums: BarcodeFormat.
- `Modules/ShapeDetection/DetectedBarcode.idl` **[R15]** - dictionaries: DetectedBarcode.
- `Modules/ShapeDetection/DetectedFace.idl` **[R15]** - dictionaries: DetectedFace.
- `Modules/ShapeDetection/DetectedText.idl` **[R15]** - dictionaries: DetectedText.
- `Modules/ShapeDetection/FaceDetector.idl` **[R15]** - FaceDetector: constructor, detect.
- `Modules/ShapeDetection/FaceDetectorOptions.idl` **[R15]** - dictionaries: FaceDetectorOptions.
- `Modules/ShapeDetection/Landmark.idl` **[R15]** - dictionaries: Landmark.
- `Modules/ShapeDetection/LandmarkType.idl` **[R15]** - enums: LandmarkType.
- `Modules/ShapeDetection/Point2D.idl` **[R15]** - dictionaries: Point2D.
- `Modules/ShapeDetection/TextDetector.idl` **[R15]** - TextDetector: constructor, detect.
### Modules/speech

- `Modules/speech/DOMWindow+SpeechSynthesis.idl` **[B11]** - DOMWindow (partial): speechSynthesis.
- `Modules/speech/SpeechRecognition.idl` **[B11]** - SpeechRecognition: constructor, lang, continuous, interimResults, maxAlternatives, start, stop, abort, onaudiostart, onsoundstart, onspeechstart, onspeechend, onsoundend, onaudioend, onresult, onnomatch, onerror, onstart, onend.
- `Modules/speech/SpeechRecognitionAlternative.idl` **[B11]** - SpeechRecognitionAlternative: transcript, confidence.
- `Modules/speech/SpeechRecognitionErrorCode.idl` **[B11]** - enums: SpeechRecognitionErrorCode.
- `Modules/speech/SpeechRecognitionErrorEvent.idl` **[B11]** - SpeechRecognitionErrorEvent: constructor, error, message; dictionaries: SpeechRecognitionErrorEventInit.
- `Modules/speech/SpeechRecognitionEvent.idl` **[B11]** - SpeechRecognitionEvent: constructor, resultIndex, results; dictionaries: SpeechRecognitionEventInit.
- `Modules/speech/SpeechRecognitionResult.idl` **[B11]** - SpeechRecognitionResult: length, item, isFinal.
- `Modules/speech/SpeechRecognitionResultList.idl` **[B11]** - SpeechRecognitionResultList: length, item.
- `Modules/speech/SpeechSynthesis.idl` **[B11]** - SpeechSynthesis: pending, speaking, paused, onvoiceschanged, speak, cancel, pause, resume, getVoices.
- `Modules/speech/SpeechSynthesisErrorCode.idl` **[B11]** - enums: SpeechSynthesisErrorCode.
- `Modules/speech/SpeechSynthesisErrorEvent.idl` **[B11]** - SpeechSynthesisErrorEvent: constructor, error.
- `Modules/speech/SpeechSynthesisErrorEventInit.idl` **[B11]** - dictionaries: SpeechSynthesisErrorEventInit.
- `Modules/speech/SpeechSynthesisEvent.idl` **[B11]** - SpeechSynthesisEvent: constructor, utterance, charIndex, charLength, elapsedTime, name.
- `Modules/speech/SpeechSynthesisEventInit.idl` **[B11]** - dictionaries: SpeechSynthesisEventInit.
- `Modules/speech/SpeechSynthesisUtterance.idl` **[B11]** - SpeechSynthesisUtterance: constructor, text, lang, voice, volume, rate, pitch, onstart, onend, onerror, onpause, onresume, onmark, onboundary.
- `Modules/speech/SpeechSynthesisVoice.idl` **[B11]** - SpeechSynthesisVoice: voiceURI, name, lang, localService, default.
### Modules/storage

- `Modules/storage/StorageManager.idl` **[B19]** - StorageManager: persisted, persist, estimate; dictionaries: StorageEstimate.
### Modules/streams

- `Modules/streams/ByteLengthQueuingStrategy.idl` **[H12]** - ByteLengthQueuingStrategy: constructor, highWaterMark, size; dictionaries: QueuingStrategyInit.
- `Modules/streams/CountQueuingStrategy.idl` **[H12]** - CountQueuingStrategy: constructor, highWaterMark, size.
- `Modules/streams/GenericTransformStream.idl` **[H12]** - GenericTransformStream (mixin): readable, writable.
- `Modules/streams/QueuingStrategy.idl` **[H12]** - dictionaries: QueuingStrategy.
- `Modules/streams/QueuingStrategySize.idl` **[H12]** - callbacks: QueuingStrategySize.
- `Modules/streams/ReadableByteStreamController.idl` **[H12]** - ReadableByteStreamController: byobRequest, desiredSize, close, enqueue, error.
- `Modules/streams/ReadableStream.idl` **[H12]** - ReadableStream: constructor, from, locked, cancel, getReader, pipeTo, pipeThrough, tee; dictionaries: ReadableStreamGetReaderOptions, ReadableStreamIteratorOptions, ReadableWritablePair; enums: ReadableStreamReaderMode.
- `Modules/streams/ReadableStreamBYOBReader.idl` **[H12]** - ReadableStreamBYOBReader: constructor, read, releaseLock, closed, cancel; dictionaries: ReadableStreamBYOBReaderReadOptions.
- `Modules/streams/ReadableStreamBYOBRequest.idl` **[H12]** - ReadableStreamBYOBRequest: view, respond, respondWithNewView.
- `Modules/streams/ReadableStreamDefaultController.idl` **[H12]** - ReadableStreamDefaultController: constructor, enqueue, close, error, desiredSize.
- `Modules/streams/ReadableStreamDefaultReader.idl` **[H12]** - ReadableStreamDefaultReader: constructor, read, releaseLock, closed, cancel.
- `Modules/streams/ReadableStreamReadResult.idl` **[H12]** - dictionaries: ReadableStreamReadResult.
- `Modules/streams/ReadableStreamSource.idl` **[H12]** - ReadableStreamSource: start, pull, cancel, controller.
- `Modules/streams/ReadableStreamType.idl` **[H12]** - enums: ReadableStreamType.
- `Modules/streams/StreamPipeOptions.idl` **[H12]** - dictionaries: StreamPipeOptions.
- `Modules/streams/TransformStream.idl` **[H12]** - TransformStream: constructor, readable, writable.
- `Modules/streams/TransformStreamDefaultController.idl` **[H12]** - TransformStreamDefaultController: constructor, desiredSize, enqueue, error, terminate.
- `Modules/streams/UnderlyingSource.idl` **[H12]** - dictionaries: UnderlyingSource.
- `Modules/streams/UnderlyingSourceCancelCallback.idl` **[H12]** - callbacks: UnderlyingSourceCancelCallback.
- `Modules/streams/UnderlyingSourcePullCallback.idl` **[H12]** - callbacks: UnderlyingSourcePullCallback.
- `Modules/streams/UnderlyingSourceStartCallback.idl` **[H12]** - callbacks: UnderlyingSourceStartCallback.
- `Modules/streams/WritableStream.idl` **[H12]** - WritableStream: constructor, locked, abort, close, getWriter.
- `Modules/streams/WritableStreamDefaultController.idl` **[H12]** - WritableStreamDefaultController: signal, error.
- `Modules/streams/WritableStreamDefaultWriter.idl` **[H12]** - WritableStreamDefaultWriter: constructor, closed, desiredSize, ready, abort, close, releaseLock, write.
- `Modules/streams/WritableStreamSink.idl` **[H12]** - WritableStreamSink: start, write, close, abort, controller.
### Modules/stub

- `Modules/stub/idl/ExtendableEvent.idl` **[R11, B17, B18, B19, B25]** - ExtendableEvent: no own parsed members.
- `Modules/stub/idl/ExtendableMessageEvent.idl` **[R11, B17, B18, B19, B25]** - ExtendableMessageEvent: no own parsed members.
- `Modules/stub/idl/FileSystemDirectoryHandle.idl` **[R11, B17, B18, B19, B25]** - FileSystemDirectoryHandle: no own parsed members.
- `Modules/stub/idl/FileSystemEntry.idl` **[R11, B17, B18, B19, B25]** - FileSystemEntry: no own parsed members.
- `Modules/stub/idl/FileSystemFileHandle.idl` **[R11, B17, B18, B19, B25]** - FileSystemFileHandle: no own parsed members.
- `Modules/stub/idl/FileSystemHandle.idl` **[R11, B17, B18, B19, B25]** - FileSystemHandle: no own parsed members.
- `Modules/stub/idl/GPUCanvasContext.idl` **[R11, B17, B18, B19, B25]** - GPUCanvasContext: no own parsed members.
- `Modules/stub/idl/IDBFactory.idl` **[R11, B17, B18, B19, B25]** - IDBFactory: no own parsed members.
- `Modules/stub/idl/NavigatorGPU.idl` **[R11, B17, B18, B19, B25]** - NavigatorGPU (mixin): no own parsed members.
- `Modules/stub/idl/NavigatorServiceWorker.idl` **[R11, B17, B18, B19, B25]** - NavigatorServiceWorker (mixin): no own parsed members.
- `Modules/stub/idl/PushEvent.idl` **[R11, B17, B18, B19, B25]** - PushEvent: no own parsed members.
- `Modules/stub/idl/PushSubscription.idl` **[R11, B17, B18, B19, B25]** - PushSubscription: no own parsed members.
- `Modules/stub/idl/ServiceWorker.idl` **[R11, B17, B18, B19, B25]** - ServiceWorker: no own parsed members.
- `Modules/stub/idl/ServiceWorkerGlobalScope.idl` **[R11, B17, B18, B19, B25]** - ServiceWorkerGlobalScope: no own parsed members.
- `Modules/stub/idl/SharedWorkerGlobalScope.idl` **[R11, B17, B18, B19, B25]** - SharedWorkerGlobalScope: no own parsed members.
- `Modules/stub/idl/WebSocket.idl` **[R11, B17, B18, B19, B25]** - WebSocket: no own parsed members.
- `Modules/stub/idl/WindowPush.idl` **[R11, B17, B18, B19, B25]** - WindowPush (mixin): no own parsed members.
### Modules/url-pattern

- `Modules/url-pattern/URLPattern.idl` **[H13]** - URLPattern: constructor, test, exec, protocol, username, password, hostname, port, pathname, search, hash, hasRegExpGroups.
- `Modules/url-pattern/URLPatternInit.idl` **[H13]** - dictionaries: URLPatternInit.
- `Modules/url-pattern/URLPatternOptions.idl` **[H13]** - dictionaries: URLPatternOptions.
- `Modules/url-pattern/URLPatternResult.idl` **[H13]** - dictionaries: URLPatternComponentResult, URLPatternResult.
### Modules/web-locks

- `Modules/web-locks/NavigatorLocks.idl` **[B24]** - NavigatorLocks (mixin): locks.
- `Modules/web-locks/WebLock.idl` **[B24]** - WebLock: name, mode.
- `Modules/web-locks/WebLockGrantedCallback.idl` **[B24]** - callbacks: WebLockGrantedCallback.
- `Modules/web-locks/WebLockManager.idl` **[B24]** - WebLockManager: request, query; dictionaries: WebLockOptions.
- `Modules/web-locks/WebLockManagerSnapshot.idl` **[B24]** - dictionaries: WebLockInfo, WebLockManagerSnapshot.
- `Modules/web-locks/WebLockMode.idl` **[B24]** - enums: WebLockMode.
### Modules/webaudio

- `Modules/webaudio/AnalyserNode.idl` **[B01]** - AnalyserNode: constructor, fftSize, frequencyBinCount, minDecibels, maxDecibels, smoothingTimeConstant, getFloatFrequencyData, getByteFrequencyData, getFloatTimeDomainData, getByteTimeDomainData.
- `Modules/webaudio/AnalyserOptions.idl` **[B01]** - dictionaries: AnalyserOptions.
- `Modules/webaudio/AudioBuffer.idl` **[B01]** - AudioBuffer: constructor, length, duration, sampleRate, numberOfChannels, getChannelData, copyFromChannel, copyToChannel.
- `Modules/webaudio/AudioBufferCallback.idl` **[B01]** - callbacks: AudioBufferCallback.
- `Modules/webaudio/AudioBufferOptions.idl` **[B01]** - dictionaries: AudioBufferOptions.
- `Modules/webaudio/AudioBufferSourceNode.idl` **[B01]** - AudioBufferSourceNode: constructor, buffer, playbackRate, detune, loop, loopStart, loopEnd, start.
- `Modules/webaudio/AudioBufferSourceOptions.idl` **[B01]** - dictionaries: AudioBufferSourceOptions.
- `Modules/webaudio/AudioContext.idl` **[B01]** - AudioContext: constructor, baseLatency, outputLatency, getOutputTimestamp, suspend, resume, close, createMediaElementSource, createMediaStreamSource, createMediaStreamDestination.
- `Modules/webaudio/AudioContextLatencyCategory.idl` **[B01]** - enums: AudioContextLatencyCategory.
- `Modules/webaudio/AudioContextOptions.idl` **[B01]** - dictionaries: AudioContextOptions.
- `Modules/webaudio/AudioContextState.idl` **[B01]** - enums: AudioContextState.
- `Modules/webaudio/AudioDestinationNode.idl` **[B01]** - AudioDestinationNode: maxChannelCount.
- `Modules/webaudio/AudioListener.idl` **[B01]** - AudioListener: positionX, positionY, positionZ, forwardX, forwardY, forwardZ, upX, upY, upZ, setPosition, setOrientation.
- `Modules/webaudio/AudioNode.idl` **[B01]** - AudioNode: context, numberOfInputs, numberOfOutputs, channelCount, channelCountMode, channelInterpretation, connect, disconnect.
- `Modules/webaudio/AudioNodeOptions.idl` **[B01]** - dictionaries: AudioNodeOptions.
- `Modules/webaudio/AudioParam.idl` **[B01]** - AudioParam: value, automationRate, defaultValue, minValue, maxValue, setValueAtTime, linearRampToValueAtTime, exponentialRampToValueAtTime, setTargetAtTime, setValueCurveAtTime, cancelScheduledValues, cancelAndHoldAtTime.
- `Modules/webaudio/AudioParamDescriptor.idl` **[B01]** - dictionaries: AudioParamDescriptor.
- `Modules/webaudio/AudioParamMap.idl` **[B01]** - AudioParamMap: no own parsed members; collections: readonly maplike<DOMString, AudioParam>;.
- `Modules/webaudio/AudioProcessingEvent.idl` **[B01]** - AudioProcessingEvent: constructor, playbackTime, inputBuffer, outputBuffer.
- `Modules/webaudio/AudioProcessingEventInit.idl` **[B01]** - dictionaries: AudioProcessingEventInit.
- `Modules/webaudio/AudioScheduledSourceNode.idl` **[B01]** - AudioScheduledSourceNode: onended, start, stop.
- `Modules/webaudio/AudioTimestamp.idl` **[B01]** - dictionaries: AudioTimestamp.
- `Modules/webaudio/AudioWorklet.idl` **[B01]** - AudioWorklet: no own parsed members.
- `Modules/webaudio/AudioWorkletGlobalScope.idl` **[B01]** - AudioWorkletGlobalScope: registerProcessor, currentFrame, currentTime, sampleRate.
- `Modules/webaudio/AudioWorkletNode.idl` **[B01]** - AudioWorkletNode: constructor, parameters, port, onprocessorerror.
- `Modules/webaudio/AudioWorkletNodeOptions.idl` **[B01]** - dictionaries: AudioWorkletNodeOptions.
- `Modules/webaudio/AudioWorkletProcessor.idl` **[B01]** - AudioWorkletProcessor: constructor, port.
- `Modules/webaudio/AudioWorkletProcessorConstructor.idl` **[B01]** - callbacks: AudioWorkletProcessorConstructor.
- `Modules/webaudio/AutomationRate.idl` **[B01]** - enums: AutomationRate.
- `Modules/webaudio/BaseAudioContext.idl` **[B01]** - BaseAudioContext: destination, currentTime, sampleRate, listener, audioWorklet, state, onstatechange, createBuffer, decodeAudioData, createBufferSource, createGain, createDelay, createBiquadFilter, createWaveShaper, createPanner, createConvolver, createDynamicsCompressor, createAnalyser, createScriptProcessor, createOscillator, createPeriodicWave, createConstantSource, createStereoPanner, createIIRFilter, createChannelSplitter, createChannelMerger.
- `Modules/webaudio/BiquadFilterNode.idl` **[B01]** - BiquadFilterNode: constructor, type, frequency, detune, Q, gain, getFrequencyResponse.
- `Modules/webaudio/BiquadFilterOptions.idl` **[B01]** - dictionaries: BiquadFilterOptions.
- `Modules/webaudio/BiquadFilterType.idl` **[B01]** - enums: BiquadFilterType.
- `Modules/webaudio/ChannelCountMode.idl` **[B01]** - enums: ChannelCountMode.
- `Modules/webaudio/ChannelInterpretation.idl` **[B01]** - enums: ChannelInterpretation.
- `Modules/webaudio/ChannelMergerNode.idl` **[B01]** - ChannelMergerNode: constructor.
- `Modules/webaudio/ChannelMergerOptions.idl` **[B01]** - dictionaries: ChannelMergerOptions.
- `Modules/webaudio/ChannelSplitterNode.idl` **[B01]** - ChannelSplitterNode: constructor.
- `Modules/webaudio/ChannelSplitterOptions.idl` **[B01]** - dictionaries: ChannelSplitterOptions.
- `Modules/webaudio/ConstantSourceNode.idl` **[B01]** - ConstantSourceNode: constructor, offset.
- `Modules/webaudio/ConstantSourceOptions.idl` **[B01]** - dictionaries: ConstantSourceOptions.
- `Modules/webaudio/ConvolverNode.idl` **[B01]** - ConvolverNode: constructor, buffer, normalize.
- `Modules/webaudio/ConvolverOptions.idl` **[B01]** - dictionaries: ConvolverOptions.
- `Modules/webaudio/DelayNode.idl` **[B01]** - DelayNode: constructor, delayTime.
- `Modules/webaudio/DelayOptions.idl` **[B01]** - dictionaries: DelayOptions.
- `Modules/webaudio/DistanceModelType.idl` **[B01]** - enums: DistanceModelType.
- `Modules/webaudio/DynamicsCompressorNode.idl` **[B01]** - DynamicsCompressorNode: constructor, threshold, knee, ratio, attack, release, reduction.
- `Modules/webaudio/DynamicsCompressorOptions.idl` **[B01]** - dictionaries: DynamicsCompressorOptions.
- `Modules/webaudio/GainNode.idl` **[B01]** - GainNode: constructor, gain.
- `Modules/webaudio/GainOptions.idl` **[B01]** - dictionaries: GainOptions.
- `Modules/webaudio/IIRFilterNode.idl` **[B01]** - IIRFilterNode: constructor, getFrequencyResponse.
- `Modules/webaudio/IIRFilterOptions.idl` **[B01]** - dictionaries: IIRFilterOptions.
- `Modules/webaudio/MediaElementAudioSourceNode.idl` **[B01]** - MediaElementAudioSourceNode: constructor, mediaElement.
- `Modules/webaudio/MediaElementAudioSourceOptions.idl` **[B01]** - dictionaries: MediaElementAudioSourceOptions.
- `Modules/webaudio/MediaStreamAudioDestinationNode.idl` **[B01]** - MediaStreamAudioDestinationNode: constructor, stream.
- `Modules/webaudio/MediaStreamAudioSourceNode.idl` **[B01]** - MediaStreamAudioSourceNode: constructor, mediaStream.
- `Modules/webaudio/MediaStreamAudioSourceOptions.idl` **[B01]** - dictionaries: MediaStreamAudioSourceOptions.
- `Modules/webaudio/OfflineAudioCompletionEvent.idl` **[B01]** - OfflineAudioCompletionEvent: constructor, renderedBuffer.
- `Modules/webaudio/OfflineAudioCompletionEventInit.idl` **[B01]** - dictionaries: OfflineAudioCompletionEventInit.
- `Modules/webaudio/OfflineAudioContext.idl` **[B01]** - OfflineAudioContext: constructor, startRendering, resume, suspend, length, oncomplete.
- `Modules/webaudio/OfflineAudioContextOptions.idl` **[B01]** - dictionaries: OfflineAudioContextOptions.
- `Modules/webaudio/OscillatorNode.idl` **[B01]** - OscillatorNode: constructor, type, frequency, detune, setPeriodicWave.
- `Modules/webaudio/OscillatorOptions.idl` **[B01]** - dictionaries: OscillatorOptions.
- `Modules/webaudio/OscillatorType.idl` **[B01]** - enums: OscillatorType.
- `Modules/webaudio/OverSampleType.idl` **[B01]** - enums: OverSampleType.
- `Modules/webaudio/PannerNode.idl` **[B01]** - PannerNode: constructor, panningModel, setPosition, setOrientation, distanceModel, refDistance, maxDistance, rolloffFactor, coneInnerAngle, coneOuterAngle, coneOuterGain, positionX, positionY, positionZ, orientationX, orientationY, orientationZ.
- `Modules/webaudio/PannerOptions.idl` **[B01]** - dictionaries: PannerOptions.
- `Modules/webaudio/PanningModelType.idl` **[B01]** - enums: PanningModelType.
- `Modules/webaudio/PeriodicWave.idl` **[B01]** - PeriodicWave: constructor.
- `Modules/webaudio/PeriodicWaveConstraints.idl` **[B01]** - dictionaries: PeriodicWaveConstraints.
- `Modules/webaudio/PeriodicWaveOptions.idl` **[B01]** - dictionaries: PeriodicWaveOptions.
- `Modules/webaudio/ScriptProcessorNode.idl` **[B01]** - ScriptProcessorNode: onaudioprocess, bufferSize.
- `Modules/webaudio/StereoPannerNode.idl` **[B01]** - StereoPannerNode: constructor, pan.
- `Modules/webaudio/StereoPannerOptions.idl` **[B01]** - dictionaries: StereoPannerOptions.
- `Modules/webaudio/WaveShaperNode.idl` **[B01]** - WaveShaperNode: constructor, curve, oversample.
- `Modules/webaudio/WaveShaperOptions.idl` **[B01]** - dictionaries: WaveShaperOptions.
### Modules/webauthn

- `Modules/webauthn/AllAcceptedCredentialsOptions.idl` **[B21]** - dictionaries: AllAcceptedCredentialsOptions.
- `Modules/webauthn/AttestationConveyancePreference.idl` **[B21]** - enums: AttestationConveyancePreference.
- `Modules/webauthn/AuthenticationExtensionsClientInputs.idl` **[B21]** - dictionaries: AuthenticationExtensionsClientInputs, AuthenticationExtensionsLargeBlobInputs, AuthenticationExtensionsPRFInputs, AuthenticationExtensionsPRFValues.
- `Modules/webauthn/AuthenticationExtensionsClientInputsJSON.idl` **[B21]** - dictionaries: AuthenticationExtensionsClientInputsJSON, AuthenticationExtensionsLargeBlobInputsJSON, AuthenticationExtensionsPRFInputsJSON, AuthenticationExtensionsPRFValuesJSON.
- `Modules/webauthn/AuthenticationExtensionsClientOutputs.idl` **[B21]** - dictionaries: AuthenticationExtensionsClientOutputs, AuthenticationExtensionsLargeBlobOutputs, AuthenticationExtensionsPRFOutputs, AuthenticationExtensionsPRFValues.
- `Modules/webauthn/AuthenticationExtensionsClientOutputsJSON.idl` **[B21]** - dictionaries: AuthenticationExtensionsClientOutputsJSON, AuthenticationExtensionsLargeBlobOutputsJSON, AuthenticationExtensionsPRFOutputsJSON, AuthenticationExtensionsPRFValuesJSON.
- `Modules/webauthn/AuthenticationResponseJSON.idl` **[B21]** - dictionaries: AuthenticationResponseJSON, AuthenticatorAssertionResponseJSON.
- `Modules/webauthn/AuthenticatorAssertionResponse.idl` **[B21]** - AuthenticatorAssertionResponse: authenticatorData, signature, userHandle.
- `Modules/webauthn/AuthenticatorAttachment.idl` **[B21]** - enums: AuthenticatorAttachment.
- `Modules/webauthn/AuthenticatorAttestationResponse.idl` **[B21]** - AuthenticatorAttestationResponse: attestationObject, getTransports, getAuthenticatorData, getPublicKey, getPublicKeyAlgorithm.
- `Modules/webauthn/AuthenticatorResponse.idl` **[B21]** - AuthenticatorResponse: clientDataJSON.
- `Modules/webauthn/AuthenticatorSelectionCriteria.idl` **[B21]** - dictionaries: AuthenticatorSelectionCriteria.
- `Modules/webauthn/AuthenticatorTransport.idl` **[B21]** - enums: AuthenticatorTransport.
- `Modules/webauthn/CredentialPropertiesOutput.idl` **[B21]** - dictionaries: CredentialPropertiesOutput.
- `Modules/webauthn/CurrentUserDetailsOptions.idl` **[B21]** - dictionaries: CurrentUserDetailsOptions.
- `Modules/webauthn/PublicKeyCredential.idl` **[B21]** - PublicKeyCredential: rawId, response, authenticatorAttachment, getClientExtensionResults, toJSON, isUserVerifyingPlatformAuthenticatorAvailable, getClientCapabilities, parseCreationOptionsFromJSON, parseRequestOptionsFromJSON, signalUnknownCredential, signalAllAcceptedCredentials, signalCurrentUserDetails.
- `Modules/webauthn/PublicKeyCredentialCreationOptions.idl` **[B21]** - dictionaries: PublicKeyCredentialCreationOptions.
- `Modules/webauthn/PublicKeyCredentialCreationOptionsJSON.idl` **[B21]** - dictionaries: PublicKeyCredentialCreationOptionsJSON.
- `Modules/webauthn/PublicKeyCredentialDescriptor.idl` **[B21]** - dictionaries: PublicKeyCredentialDescriptor.
- `Modules/webauthn/PublicKeyCredentialDescriptorJSON.idl` **[B21]** - dictionaries: PublicKeyCredentialDescriptorJSON.
- `Modules/webauthn/PublicKeyCredentialEntity.idl` **[B21]** - dictionaries: PublicKeyCredentialEntity.
- `Modules/webauthn/PublicKeyCredentialParameters.idl` **[B21]** - dictionaries: PublicKeyCredentialParameters.
- `Modules/webauthn/PublicKeyCredentialRequestOptions.idl` **[B21]** - dictionaries: PublicKeyCredentialRequestOptions.
- `Modules/webauthn/PublicKeyCredentialRequestOptionsJSON.idl` **[B21]** - dictionaries: PublicKeyCredentialRequestOptionsJSON.
- `Modules/webauthn/PublicKeyCredentialRpEntity.idl` **[B21]** - dictionaries: PublicKeyCredentialRpEntity.
- `Modules/webauthn/PublicKeyCredentialType.idl` **[B21]** - enums: PublicKeyCredentialType.
- `Modules/webauthn/PublicKeyCredentialUserEntity.idl` **[B21]** - dictionaries: PublicKeyCredentialUserEntity.
- `Modules/webauthn/PublicKeyCredentialUserEntityJSON.idl` **[B21]** - dictionaries: PublicKeyCredentialUserEntityJSON.
- `Modules/webauthn/RegistrationResponseJSON.idl` **[B21]** - dictionaries: AuthenticatorAttestationResponseJSON, RegistrationResponseJSON.
- `Modules/webauthn/ResidentKeyRequirement.idl` **[B21]** - enums: ResidentKeyRequirement.
- `Modules/webauthn/UnknownCredentialOptions.idl` **[B21]** - dictionaries: UnknownCredentialOptions.
- `Modules/webauthn/UserVerificationRequirement.idl` **[B21]** - enums: UserVerificationRequirement.
### Modules/webcodecs

- `Modules/webcodecs/AacEncoderConfig.idl` **[B03]** - dictionaries: AacEncoderConfig; enums: AacBitstreamFormat.
- `Modules/webcodecs/AudioSampleFormat.idl` **[B03]** - enums: AudioSampleFormat.
- `Modules/webcodecs/AvcEncoderConfig.idl` **[B03]** - dictionaries: AvcEncoderConfig; enums: AvcBitstreamFormat.
- `Modules/webcodecs/BitrateMode.idl` **[B03]** - enums: BitrateMode.
- `Modules/webcodecs/FlacEncoderConfig.idl` **[B03]** - dictionaries: FlacEncoderConfig.
- `Modules/webcodecs/HardwareAcceleration.idl` **[B03]** - enums: HardwareAcceleration.
- `Modules/webcodecs/LatencyMode.idl` **[B03]** - enums: LatencyMode.
- `Modules/webcodecs/OpusEncoderConfig.idl` **[B03]** - dictionaries: OpusEncoderConfig; enums: OpusBitstreamFormat.
- `Modules/webcodecs/PlaneLayout.idl` **[B03]** - dictionaries: PlaneLayout.
- `Modules/webcodecs/VideoColorPrimaries.idl` **[B03]** - enums: VideoColorPrimaries.
- `Modules/webcodecs/VideoColorSpace.idl` **[B03]** - VideoColorSpace: constructor, primaries, transfer, matrix, fullRange, toJSON.
- `Modules/webcodecs/VideoColorSpaceInit.idl` **[B03]** - dictionaries: VideoColorSpaceInit.
- `Modules/webcodecs/VideoMatrixCoefficients.idl` **[B03]** - enums: VideoMatrixCoefficients.
- `Modules/webcodecs/VideoPixelFormat.idl` **[B03]** - enums: VideoPixelFormat.
- `Modules/webcodecs/VideoTransferCharacteristics.idl` **[B03]** - enums: VideoTransferCharacteristics.
- `Modules/webcodecs/WebCodecsAlphaOption.idl` **[B03]** - enums: WebCodecsAlphaOption.
- `Modules/webcodecs/WebCodecsAudioData.idl` **[B03]** - WebCodecsAudioData: constructor, format, sampleRate, numberOfFrames, numberOfChannels, duration, timestamp, allocationSize, copyTo, clone, close; dictionaries: AudioDataCopyToOptions, AudioDataInit.
- `Modules/webcodecs/WebCodecsAudioDataOutputCallback.idl` **[B03]** - callbacks: WebCodecsAudioDataOutputCallback.
- `Modules/webcodecs/WebCodecsAudioDecoder.idl` **[B03]** - WebCodecsAudioDecoder: constructor, state, decodeQueueSize, ondequeue, configure, decode, flush, reset, close, isConfigSupported; dictionaries: WebCodecsAudioDecoderInit.
- `Modules/webcodecs/WebCodecsAudioDecoderConfig.idl` **[B03]** - dictionaries: WebCodecsAudioDecoderConfig.
- `Modules/webcodecs/WebCodecsAudioDecoderSupport.idl` **[B03]** - dictionaries: WebCodecsAudioDecoderSupport.
- `Modules/webcodecs/WebCodecsAudioEncoder.idl` **[B03]** - WebCodecsAudioEncoder: constructor, state, encodeQueueSize, ondequeue, configure, encode, flush, reset, close, isConfigSupported; dictionaries: WebCodecsAudioEncoderInit.
- `Modules/webcodecs/WebCodecsAudioEncoderConfig.idl` **[B03]** - dictionaries: WebCodecsAudioEncoderConfig.
- `Modules/webcodecs/WebCodecsAudioEncoderSupport.idl` **[B03]** - dictionaries: WebCodecsAudioEncoderSupport.
- `Modules/webcodecs/WebCodecsCodecState.idl` **[B03]** - enums: WebCodecsCodecState.
- `Modules/webcodecs/WebCodecsEncodedAudioChunk.idl` **[B03]** - WebCodecsEncodedAudioChunk: constructor, type, timestamp, duration, byteLength, copyTo; dictionaries: WebCodecsEncodedAudioChunkInit.
- `Modules/webcodecs/WebCodecsEncodedAudioChunkMetadata.idl` **[B03]** - dictionaries: WebCodecsEncodedAudioChunkMetadata.
- `Modules/webcodecs/WebCodecsEncodedAudioChunkOutputCallback.idl` **[B03]** - callbacks: WebCodecsEncodedAudioChunkOutputCallback.
- `Modules/webcodecs/WebCodecsEncodedAudioChunkType.idl` **[B03]** - enums: WebCodecsEncodedAudioChunkType.
- `Modules/webcodecs/WebCodecsEncodedVideoChunk.idl` **[B03]** - WebCodecsEncodedVideoChunk: constructor, type, timestamp, duration, byteLength, copyTo; dictionaries: WebCodecsEncodedVideoChunkInit.
- `Modules/webcodecs/WebCodecsEncodedVideoChunkMetadata.idl` **[B03]** - dictionaries: WebCodecsEncodedVideoChunkMetadata.
- `Modules/webcodecs/WebCodecsEncodedVideoChunkOutputCallback.idl` **[B03]** - callbacks: WebCodecsEncodedVideoChunkOutputCallback.
- `Modules/webcodecs/WebCodecsEncodedVideoChunkType.idl` **[B03]** - enums: WebCodecsEncodedVideoChunkType.
- `Modules/webcodecs/WebCodecsErrorCallback.idl` **[B03]** - callbacks: WebCodecsErrorCallback.
- `Modules/webcodecs/WebCodecsSvcOutputMetadata.idl` **[B03]** - dictionaries: WebCodecsSvcOutputMetadata.
- `Modules/webcodecs/WebCodecsVideoDecoder.idl` **[B03]** - WebCodecsVideoDecoder: constructor, state, decodeQueueSize, ondequeue, configure, decode, flush, reset, close, isConfigSupported; dictionaries: WebCodecsVideoDecoderInit.
- `Modules/webcodecs/WebCodecsVideoDecoderConfig.idl` **[B03]** - dictionaries: WebCodecsVideoDecoderConfig.
- `Modules/webcodecs/WebCodecsVideoDecoderSupport.idl` **[B03]** - dictionaries: WebCodecsVideoDecoderSupport.
- `Modules/webcodecs/WebCodecsVideoEncoder.idl` **[B03]** - WebCodecsVideoEncoder: constructor, state, encodeQueueSize, ondequeue, configure, encode, flush, reset, close, isConfigSupported; dictionaries: WebCodecsVideoEncoderInit.
- `Modules/webcodecs/WebCodecsVideoEncoderConfig.idl` **[B03]** - dictionaries: WebCodecsVideoEncoderConfig.
- `Modules/webcodecs/WebCodecsVideoEncoderEncodeOptions.idl` **[B03]** - dictionaries: WebCodecsVideoEncoderEncodeOptions.
- `Modules/webcodecs/WebCodecsVideoEncoderSupport.idl` **[B03]** - dictionaries: WebCodecsVideoEncoderSupport.
- `Modules/webcodecs/WebCodecsVideoFrame.idl` **[B03]** - WebCodecsVideoFrame: constructor, format, codedWidth, codedHeight, codedRect, visibleRect, displayWidth, displayHeight, duration, timestamp, colorSpace, allocationSize, copyTo, clone, close; dictionaries: VideoFrameBufferInit, VideoFrameCopyToOptions, VideoFrameInit.
- `Modules/webcodecs/WebCodecsVideoFrameOutputCallback.idl` **[B03]** - callbacks: WebCodecsVideoFrameOutputCallback.
### Modules/webdatabase

- `Modules/webdatabase/Database.idl` **[B19]** - Database: version, changeVersion, transaction, readTransaction.
- `Modules/webdatabase/DatabaseCallback.idl` **[B19]** - callbacks: DatabaseCallback.
- `Modules/webdatabase/DOMWindow+WebDatabase.idl` **[B19]** - DOMWindow (partial): openDatabase.
- `Modules/webdatabase/SQLError.idl` **[B19]** - SQLError: code, message.
- `Modules/webdatabase/SQLResultSet.idl` **[B19]** - SQLResultSet: rows, insertId, rowsAffected.
- `Modules/webdatabase/SQLResultSetRowList.idl` **[B19]** - SQLResultSetRowList: length, item.
- `Modules/webdatabase/SQLStatementCallback.idl` **[B19]** - callbacks: SQLStatementCallback.
- `Modules/webdatabase/SQLStatementErrorCallback.idl` **[B19]** - callbacks: SQLStatementErrorCallback.
- `Modules/webdatabase/SQLTransaction.idl` **[B19]** - SQLTransaction: executeSql.
- `Modules/webdatabase/SQLTransactionCallback.idl` **[B19]** - callbacks: SQLTransactionCallback.
- `Modules/webdatabase/SQLTransactionErrorCallback.idl` **[B19]** - callbacks: SQLTransactionErrorCallback.
### Modules/webdriver

- `Modules/webdriver/Navigator+WebDriver.idl` **[B23]** - Navigator (partial): webdriver.
### Modules/WebGPU

- `Modules/WebGPU/GPU.idl` **[R11]** - GPU: requestAdapter, getPreferredCanvasFormat, wgslLanguageFeatures.
- `Modules/WebGPU/GPUAdapter.idl` **[R11]** - GPUAdapter: features, limits, info, isFallbackAdapter, requestDevice.
- `Modules/WebGPU/GPUAdapterInfo.idl` **[R11]** - GPUAdapterInfo: vendor, architecture, device, description, isFallbackAdapter, subgroupMinSize, subgroupMaxSize.
- `Modules/WebGPU/GPUAddressMode.idl` **[R11]** - enums: GPUAddressMode.
- `Modules/WebGPU/GPUAutoLayoutMode.idl` **[R11]** - enums: GPUAutoLayoutMode.
- `Modules/WebGPU/GPUBindGroup.idl` **[R11]** - GPUBindGroup: no own parsed members; includes: GPUBindGroup -> GPUObjectBase.
- `Modules/WebGPU/GPUBindGroupDescriptor.idl` **[R11]** - dictionaries: GPUBindGroupDescriptor.
- `Modules/WebGPU/GPUBindGroupEntry.idl` **[R11]** - dictionaries: GPUBindGroupEntry.
- `Modules/WebGPU/GPUBindGroupLayout.idl` **[R11]** - GPUBindGroupLayout: no own parsed members; includes: GPUBindGroupLayout -> GPUObjectBase.
- `Modules/WebGPU/GPUBindGroupLayoutDescriptor.idl` **[R11]** - dictionaries: GPUBindGroupLayoutDescriptor.
- `Modules/WebGPU/GPUBindGroupLayoutEntry.idl` **[R11]** - dictionaries: GPUBindGroupLayoutEntry.
- `Modules/WebGPU/GPUBlendComponent.idl` **[R11]** - dictionaries: GPUBlendComponent.
- `Modules/WebGPU/GPUBlendFactor.idl` **[R11]** - enums: GPUBlendFactor.
- `Modules/WebGPU/GPUBlendOperation.idl` **[R11]** - enums: GPUBlendOperation.
- `Modules/WebGPU/GPUBlendState.idl` **[R11]** - dictionaries: GPUBlendState.
- `Modules/WebGPU/GPUBuffer.idl` **[R11]** - GPUBuffer: size, usage, mapState, mapAsync, getMappedRange, unmap, destroy; includes: GPUBuffer -> GPUObjectBase.
- `Modules/WebGPU/GPUBufferBinding.idl` **[R11]** - dictionaries: GPUBufferBinding.
- `Modules/WebGPU/GPUBufferBindingLayout.idl` **[R11]** - dictionaries: GPUBufferBindingLayout.
- `Modules/WebGPU/GPUBufferBindingType.idl` **[R11]** - enums: GPUBufferBindingType.
- `Modules/WebGPU/GPUBufferDescriptor.idl` **[R11]** - dictionaries: GPUBufferDescriptor.
- `Modules/WebGPU/GPUBufferMapState.idl` **[R11]** - enums: GPUBufferMapState.
- `Modules/WebGPU/GPUBufferUsage.idl` **[R11]** - namespace statements: const GPUFlagsConstant MAP_READ = 0x0001; const GPUFlagsConstant MAP_WRITE = 0x0002; const GPUFlagsConstant COPY_SRC = 0x0004; const GPUFlagsConstant COPY_DST = 0x0008; const GPUFlagsConstant INDEX = 0x0010; const GPUFlagsConstant VERTEX = 0x0020; const GPUFlagsConstant UNIFORM = 0x0040; const GPUFlagsConstant STORAGE = 0x0080; const GPUFlagsConstant INDIRECT = 0x0100; const GPUFlagsConstant QUERY_RESOLVE = 0x0200;.
- `Modules/WebGPU/GPUCanvasAlphaMode.idl` **[R11]** - enums: GPUCanvasAlphaMode.
- `Modules/WebGPU/GPUCanvasConfiguration.idl` **[R11]** - dictionaries: GPUCanvasConfiguration.
- `Modules/WebGPU/GPUCanvasToneMapping.idl` **[R11]** - dictionaries: GPUCanvasToneMapping.
- `Modules/WebGPU/GPUCanvasToneMappingMode.idl` **[R11]** - enums: GPUCanvasToneMappingMode.
- `Modules/WebGPU/GPUColorDict.idl` **[R11]** - dictionaries: GPUColorDict.
- `Modules/WebGPU/GPUColorTargetState.idl` **[R11]** - dictionaries: GPUColorTargetState.
- `Modules/WebGPU/GPUColorWrite.idl` **[R11]** - namespace statements: const GPUFlagsConstant RED = 0x1; const GPUFlagsConstant GREEN = 0x2; const GPUFlagsConstant BLUE = 0x4; const GPUFlagsConstant ALPHA = 0x8; const GPUFlagsConstant ALL = 0xF;.
- `Modules/WebGPU/GPUCommandBuffer.idl` **[R11]** - GPUCommandBuffer: no own parsed members; includes: GPUCommandBuffer -> GPUObjectBase.
- `Modules/WebGPU/GPUCommandBufferDescriptor.idl` **[R11]** - dictionaries: GPUCommandBufferDescriptor.
- `Modules/WebGPU/GPUCommandEncoder.idl` **[R11]** - GPUCommandEncoder: beginRenderPass, beginComputePass, copyBufferToBuffer, copyBufferToTexture, copyTextureToBuffer, copyTextureToTexture, clearBuffer, resolveQuerySet, finish; includes: GPUCommandEncoder -> GPUObjectBase, GPUCommandEncoder -> GPUCommandsMixin, GPUCommandEncoder -> GPUDebugCommandsMixin.
- `Modules/WebGPU/GPUCommandEncoderDescriptor.idl` **[R11]** - dictionaries: GPUCommandEncoderDescriptor.
- `Modules/WebGPU/GPUCommandsMixin.idl` **[R11]** - GPUCommandsMixin (mixin): no own parsed members.
- `Modules/WebGPU/GPUCompareFunction.idl` **[R11]** - enums: GPUCompareFunction.
- `Modules/WebGPU/GPUCompilationInfo.idl` **[R11]** - GPUCompilationInfo: messages.
- `Modules/WebGPU/GPUCompilationMessage.idl` **[R11]** - GPUCompilationMessage: message, type, lineNum, linePos, offset, length.
- `Modules/WebGPU/GPUCompilationMessageType.idl` **[R11]** - enums: GPUCompilationMessageType.
- `Modules/WebGPU/GPUComputePassDescriptor.idl` **[R11]** - dictionaries: GPUComputePassDescriptor.
- `Modules/WebGPU/GPUComputePassEncoder.idl` **[R11]** - GPUComputePassEncoder: setPipeline, dispatchWorkgroups, dispatchWorkgroupsIndirect, end; includes: GPUComputePassEncoder -> GPUObjectBase, GPUComputePassEncoder -> GPUCommandsMixin, GPUComputePassEncoder -> GPUDebugCommandsMixin, GPUComputePassEncoder -> GPUProgrammablePassEncoder.
- `Modules/WebGPU/GPUComputePassTimestampWrites.idl` **[R11]** - dictionaries: GPUComputePassTimestampWrites.
- `Modules/WebGPU/GPUComputePipeline.idl` **[R11]** - GPUComputePipeline: no own parsed members; includes: GPUComputePipeline -> GPUObjectBase, GPUComputePipeline -> GPUPipelineBase.
- `Modules/WebGPU/GPUComputePipelineDescriptor.idl` **[R11]** - dictionaries: GPUComputePipelineDescriptor.
- `Modules/WebGPU/GPUCullMode.idl` **[R11]** - enums: GPUCullMode.
- `Modules/WebGPU/GPUDebugCommandsMixin.idl` **[R11]** - GPUDebugCommandsMixin (mixin): pushDebugGroup, popDebugGroup, insertDebugMarker.
- `Modules/WebGPU/GPUDepthStencilState.idl` **[R11]** - dictionaries: GPUDepthStencilState.
- `Modules/WebGPU/GPUDevice.idl` **[R11]** - GPUDevice: features, limits, adapterInfo, queue, onuncapturederror, destroy, createBuffer, createTexture, createSampler, importExternalTexture, createBindGroupLayout, createPipelineLayout, createBindGroup, createShaderModule, createComputePipeline, createRenderPipeline, createComputePipelineAsync, createRenderPipelineAsync, createCommandEncoder, createRenderBundleEncoder, createQuerySet; includes: GPUDevice -> GPUObjectBase.
- `Modules/WebGPU/GPUDeviceDescriptor.idl` **[R11]** - dictionaries: GPUDeviceDescriptor.
- `Modules/WebGPU/GPUDeviceError.idl` **[R11]** - GPUDevice (partial): pushErrorScope, popErrorScope.
- `Modules/WebGPU/GPUDeviceLost.idl` **[R11]** - GPUDevice (partial): lost.
- `Modules/WebGPU/GPUDeviceLostInfo.idl` **[R11]** - GPUDeviceLostInfo: reason, message.
- `Modules/WebGPU/GPUDeviceLostReason.idl` **[R11]** - enums: GPUDeviceLostReason.
- `Modules/WebGPU/GPUDeviceUncapturedError.idl` **[R11]** - GPUDevice (partial): no own parsed members.
- `Modules/WebGPU/GPUErrorFilter.idl` **[R11]** - enums: GPUErrorFilter.
- `Modules/WebGPU/GPUExtent3DDict.idl` **[R11]** - dictionaries: GPUExtent3DDict.
- `Modules/WebGPU/GPUExternalTexture.idl` **[R11]** - GPUExternalTexture: no own parsed members; includes: GPUExternalTexture -> GPUObjectBase.
- `Modules/WebGPU/GPUExternalTextureBindingLayout.idl` **[R11]** - dictionaries: GPUExternalTextureBindingLayout.
- `Modules/WebGPU/GPUExternalTextureDescriptor.idl` **[R11]** - dictionaries: GPUExternalTextureDescriptor.
- `Modules/WebGPU/GPUFeatureName.idl` **[R11]** - enums: GPUFeatureName.
- `Modules/WebGPU/GPUFilterMode.idl` **[R11]** - enums: GPUFilterMode.
- `Modules/WebGPU/GPUFragmentState.idl` **[R11]** - dictionaries: GPUFragmentState.
- `Modules/WebGPU/GPUFrontFace.idl` **[R11]** - enums: GPUFrontFace.
- `Modules/WebGPU/GPUImageCopyBuffer.idl` **[R11]** - dictionaries: GPUImageCopyBuffer.
- `Modules/WebGPU/GPUImageCopyExternalImage.idl` **[R11]** - dictionaries: GPUImageCopyExternalImage.
- `Modules/WebGPU/GPUImageCopyTexture.idl` **[R11]** - dictionaries: GPUImageCopyTexture.
- `Modules/WebGPU/GPUImageCopyTextureTagged.idl` **[R11]** - dictionaries: GPUImageCopyTextureTagged.
- `Modules/WebGPU/GPUImageDataLayout.idl` **[R11]** - dictionaries: GPUImageDataLayout.
- `Modules/WebGPU/GPUIndexFormat.idl` **[R11]** - enums: GPUIndexFormat.
- `Modules/WebGPU/GPUInternalError.idl` **[R11]** - GPUInternalError: constructor, message, stack.
- `Modules/WebGPU/GPULoadOp.idl` **[R11]** - enums: GPULoadOp.
- `Modules/WebGPU/GPUMapMode.idl` **[R11]** - namespace statements: const GPUFlagsConstant READ = 0x0001; const GPUFlagsConstant WRITE = 0x0002;.
- `Modules/WebGPU/GPUMipmapFilterMode.idl` **[R11]** - enums: GPUMipmapFilterMode.
- `Modules/WebGPU/GPUMultisampleState.idl` **[R11]** - dictionaries: GPUMultisampleState.
- `Modules/WebGPU/GPUObjectBase.idl` **[R11]** - GPUObjectBase (mixin): label.
- `Modules/WebGPU/GPUObjectDescriptorBase.idl` **[R11]** - dictionaries: GPUObjectDescriptorBase.
- `Modules/WebGPU/GPUOrigin2DDict.idl` **[R11]** - dictionaries: GPUOrigin2DDict.
- `Modules/WebGPU/GPUOrigin3DDict.idl` **[R11]** - dictionaries: GPUOrigin3DDict.
- `Modules/WebGPU/GPUOutOfMemoryError.idl` **[R11]** - GPUOutOfMemoryError: constructor, message.
- `Modules/WebGPU/GPUPipelineBase.idl` **[R11]** - GPUPipelineBase (mixin): getBindGroupLayout.
- `Modules/WebGPU/GPUPipelineDescriptorBase.idl` **[R11]** - dictionaries: GPUPipelineDescriptorBase.
- `Modules/WebGPU/GPUPipelineError.idl` **[R11]** - GPUPipelineError: constructor, reason, stack.
- `Modules/WebGPU/GPUPipelineErrorInit.idl` **[R11]** - dictionaries: GPUPipelineErrorInit.
- `Modules/WebGPU/GPUPipelineErrorReason.idl` **[R11]** - enums: GPUPipelineErrorReason.
- `Modules/WebGPU/GPUPipelineLayout.idl` **[R11]** - GPUPipelineLayout: no own parsed members; includes: GPUPipelineLayout -> GPUObjectBase.
- `Modules/WebGPU/GPUPipelineLayoutDescriptor.idl` **[R11]** - dictionaries: GPUPipelineLayoutDescriptor.
- `Modules/WebGPU/GPUPowerPreference.idl` **[R11]** - enums: GPUPowerPreference.
- `Modules/WebGPU/GPUPrimitiveState.idl` **[R11]** - dictionaries: GPUPrimitiveState.
- `Modules/WebGPU/GPUPrimitiveTopology.idl` **[R11]** - enums: GPUPrimitiveTopology.
- `Modules/WebGPU/GPUProgrammablePassEncoder.idl` **[R11]** - GPUProgrammablePassEncoder (mixin): setBindGroup.
- `Modules/WebGPU/GPUProgrammableStage.idl` **[R11]** - dictionaries: GPUProgrammableStage.
- `Modules/WebGPU/GPUQuerySet.idl` **[R11]** - GPUQuerySet: destroy, type, count; includes: GPUQuerySet -> GPUObjectBase.
- `Modules/WebGPU/GPUQuerySetDescriptor.idl` **[R11]** - dictionaries: GPUQuerySetDescriptor.
- `Modules/WebGPU/GPUQueryType.idl` **[R11]** - enums: GPUQueryType.
- `Modules/WebGPU/GPUQueue.idl` **[R11]** - GPUQueue: submit, onSubmittedWorkDone, writeBuffer, writeTexture, copyExternalImageToTexture; includes: GPUQueue -> GPUObjectBase.
- `Modules/WebGPU/GPUQueueDescriptor.idl` **[R11]** - dictionaries: GPUQueueDescriptor.
- `Modules/WebGPU/GPURenderBundle.idl` **[R11]** - GPURenderBundle: no own parsed members; includes: GPURenderBundle -> GPUObjectBase.
- `Modules/WebGPU/GPURenderBundleDescriptor.idl` **[R11]** - dictionaries: GPURenderBundleDescriptor.
- `Modules/WebGPU/GPURenderBundleEncoder.idl` **[R11]** - GPURenderBundleEncoder: finish; includes: GPURenderBundleEncoder -> GPUObjectBase, GPURenderBundleEncoder -> GPUCommandsMixin, GPURenderBundleEncoder -> GPUDebugCommandsMixin, GPURenderBundleEncoder -> GPUProgrammablePassEncoder, GPURenderBundleEncoder -> GPURenderEncoderBase.
- `Modules/WebGPU/GPURenderBundleEncoderDescriptor.idl` **[R11]** - dictionaries: GPURenderBundleEncoderDescriptor.
- `Modules/WebGPU/GPURenderEncoderBase.idl` **[R11]** - GPURenderEncoderBase (mixin): setPipeline, setIndexBuffer, setVertexBuffer, draw, drawIndexed, drawIndirect, drawIndexedIndirect.
- `Modules/WebGPU/GPURenderPassColorAttachment.idl` **[R11]** - dictionaries: GPURenderPassColorAttachment.
- `Modules/WebGPU/GPURenderPassDepthStencilAttachment.idl` **[R11]** - dictionaries: GPURenderPassDepthStencilAttachment.
- `Modules/WebGPU/GPURenderPassDescriptor.idl` **[R11]** - dictionaries: GPURenderPassDescriptor.
- `Modules/WebGPU/GPURenderPassEncoder.idl` **[R11]** - GPURenderPassEncoder: setViewport, setScissorRect, setBlendConstant, setStencilReference, beginOcclusionQuery, endOcclusionQuery, executeBundles, end; includes: GPURenderPassEncoder -> GPUObjectBase, GPURenderPassEncoder -> GPUCommandsMixin, GPURenderPassEncoder -> GPUDebugCommandsMixin, GPURenderPassEncoder -> GPUProgrammablePassEncoder, GPURenderPassEncoder -> GPURenderEncoderBase.
- `Modules/WebGPU/GPURenderPassLayout.idl` **[R11]** - dictionaries: GPURenderPassLayout.
- `Modules/WebGPU/GPURenderPassTimestampWrites.idl` **[R11]** - dictionaries: GPURenderPassTimestampWrites.
- `Modules/WebGPU/GPURenderPipeline.idl` **[R11]** - GPURenderPipeline: no own parsed members; includes: GPURenderPipeline -> GPUObjectBase, GPURenderPipeline -> GPUPipelineBase.
- `Modules/WebGPU/GPURenderPipelineDescriptor.idl` **[R11]** - dictionaries: GPURenderPipelineDescriptor.
- `Modules/WebGPU/GPURequestAdapterOptions.idl` **[R11]** - dictionaries: GPURequestAdapterOptions.
- `Modules/WebGPU/GPUSampler.idl` **[R11]** - GPUSampler: no own parsed members; includes: GPUSampler -> GPUObjectBase.
- `Modules/WebGPU/GPUSamplerBindingLayout.idl` **[R11]** - dictionaries: GPUSamplerBindingLayout.
- `Modules/WebGPU/GPUSamplerBindingType.idl` **[R11]** - enums: GPUSamplerBindingType.
- `Modules/WebGPU/GPUSamplerDescriptor.idl` **[R11]** - dictionaries: GPUSamplerDescriptor.
- `Modules/WebGPU/GPUShaderModule.idl` **[R11]** - GPUShaderModule: getCompilationInfo; includes: GPUShaderModule -> GPUObjectBase.
- `Modules/WebGPU/GPUShaderModuleCompilationHint.idl` **[R11]** - dictionaries: GPUShaderModuleCompilationHint.
- `Modules/WebGPU/GPUShaderModuleDescriptor.idl` **[R11]** - dictionaries: GPUShaderModuleDescriptor.
- `Modules/WebGPU/GPUShaderStage.idl` **[R11]** - namespace statements: const GPUFlagsConstant VERTEX = 0x1; const GPUFlagsConstant FRAGMENT = 0x2; const GPUFlagsConstant COMPUTE = 0x4;.
- `Modules/WebGPU/GPUStencilFaceState.idl` **[R11]** - dictionaries: GPUStencilFaceState.
- `Modules/WebGPU/GPUStencilOperation.idl` **[R11]** - enums: GPUStencilOperation.
- `Modules/WebGPU/GPUStorageTextureAccess.idl` **[R11]** - enums: GPUStorageTextureAccess.
- `Modules/WebGPU/GPUStorageTextureBindingLayout.idl` **[R11]** - dictionaries: GPUStorageTextureBindingLayout.
- `Modules/WebGPU/GPUStoreOp.idl` **[R11]** - enums: GPUStoreOp.
- `Modules/WebGPU/GPUSupportedFeatures.idl` **[R11]** - GPUSupportedFeatures: no own parsed members; collections: readonly setlike<DOMString>;.
- `Modules/WebGPU/GPUSupportedLimits.idl` **[R11]** - GPUSupportedLimits: maxTextureDimension1D, maxTextureDimension2D, maxTextureDimension3D, maxTextureArrayLayers, maxBindGroups, maxBindGroupsPlusVertexBuffers, maxBindingsPerBindGroup, maxDynamicUniformBuffersPerPipelineLayout, maxDynamicStorageBuffersPerPipelineLayout, maxSampledTexturesPerShaderStage, maxSamplersPerShaderStage, maxStorageBuffersPerShaderStage, maxStorageTexturesPerShaderStage, maxUniformBuffersPerShaderStage, maxUniformBufferBindingSize, maxStorageBufferBindingSize, minUniformBufferOffsetAlignment, minStorageBufferOffsetAlignment, maxVertexBuffers, maxBufferSize, maxVertexAttributes, maxVertexBufferArrayStride, maxInterStageShaderVariables, maxColorAttachments, maxColorAttachmentBytesPerSample, maxComputeWorkgroupStorageSize, maxComputeInvocationsPerWorkgroup, maxComputeWorkgroupSizeX, maxComputeWorkgroupSizeY, maxComputeWorkgroupSizeZ, maxComputeWorkgroupsPerDimension, maxStorageBuffersInFragmentStage, maxStorageTexturesInFragmentStage, maxStorageBuffersInVertexStage, maxStorageTexturesInVertexStage.
- `Modules/WebGPU/GPUTexture.idl` **[R11]** - GPUTexture: createView, destroy, width, height, depthOrArrayLayers, mipLevelCount, sampleCount, dimension, format, usage; includes: GPUTexture -> GPUObjectBase.
- `Modules/WebGPU/GPUTextureAspect.idl` **[R11]** - enums: GPUTextureAspect.
- `Modules/WebGPU/GPUTextureBindingLayout.idl` **[R11]** - dictionaries: GPUTextureBindingLayout.
- `Modules/WebGPU/GPUTextureDescriptor.idl` **[R11]** - dictionaries: GPUTextureDescriptor.
- `Modules/WebGPU/GPUTextureDimension.idl` **[R11]** - enums: GPUTextureDimension.
- `Modules/WebGPU/GPUTextureFormat.idl` **[R11]** - enums: GPUTextureFormat.
- `Modules/WebGPU/GPUTextureSampleType.idl` **[R11]** - enums: GPUTextureSampleType.
- `Modules/WebGPU/GPUTextureUsage.idl` **[R11]** - namespace statements: const GPUFlagsConstant COPY_SRC = 0x01; const GPUFlagsConstant COPY_DST = 0x02; const GPUFlagsConstant TEXTURE_BINDING = 0x04; const GPUFlagsConstant STORAGE_BINDING = 0x08; const GPUFlagsConstant RENDER_ATTACHMENT = 0x10; const GPUFlagsConstant TRANSIENT_ATTACHMENT = 0x20;.
- `Modules/WebGPU/GPUTextureView.idl` **[R11]** - GPUTextureView: no own parsed members; includes: GPUTextureView -> GPUObjectBase.
- `Modules/WebGPU/GPUTextureViewDescriptor.idl` **[R11]** - dictionaries: GPUTextureViewDescriptor.
- `Modules/WebGPU/GPUTextureViewDimension.idl` **[R11]** - enums: GPUTextureViewDimension.
- `Modules/WebGPU/GPUUncapturedErrorEvent.idl` **[R11]** - GPUUncapturedErrorEvent: constructor, error.
- `Modules/WebGPU/GPUUncapturedErrorEventInit.idl` **[R11]** - dictionaries: GPUUncapturedErrorEventInit.
- `Modules/WebGPU/GPUValidationError.idl` **[R11]** - GPUValidationError: constructor, message, stack.
- `Modules/WebGPU/GPUVertexAttribute.idl` **[R11]** - dictionaries: GPUVertexAttribute.
- `Modules/WebGPU/GPUVertexBufferLayout.idl` **[R11]** - dictionaries: GPUVertexBufferLayout.
- `Modules/WebGPU/GPUVertexFormat.idl` **[R11]** - enums: GPUVertexFormat.
- `Modules/WebGPU/GPUVertexState.idl` **[R11]** - dictionaries: GPUVertexState.
- `Modules/WebGPU/GPUVertexStepMode.idl` **[R11]** - enums: GPUVertexStepMode.
- `Modules/WebGPU/NavigatorGPU.idl` **[R11]** - NavigatorGPU (mixin): gpu.
- `Modules/WebGPU/WGSLLanguageFeatures.idl` **[R11]** - WGSLLanguageFeatures: no own parsed members; collections: readonly setlike<DOMString>;.
### Modules/websockets

- `Modules/websockets/CloseEvent.idl` **[B18]** - CloseEvent: constructor, wasClean, code, reason; dictionaries: CloseEventInit.
- `Modules/websockets/WebSocket.idl` **[B18]** - WebSocket: constructor, url, readyState, bufferedAmount, onopen, onerror, onclose, protocol, extensions, close, onmessage, binaryType, send; enums: BinaryType.
### Modules/webtransport

- `Modules/webtransport/WebTransport.idl` **[B18]** - WebTransport: constructor, getStats, ready, reliability, congestionControl, anticipatedConcurrentIncomingUnidirectionalStreams, anticipatedConcurrentIncomingBidirectionalStreams, protocol, closed, draining, close, datagrams, createBidirectionalStream, incomingBidirectionalStreams, createUnidirectionalStream, incomingUnidirectionalStreams, createSendGroup, supportsReliableOnly.
- `Modules/webtransport/WebTransportBidirectionalStream.idl` **[B18]** - WebTransportBidirectionalStream: readable, writable.
- `Modules/webtransport/WebTransportCloseInfo.idl` **[B18]** - dictionaries: WebTransportCloseInfo.
- `Modules/webtransport/WebTransportCongestionControl.idl` **[B18]** - enums: WebTransportCongestionControl.
- `Modules/webtransport/WebTransportConnectionStats.idl` **[B18]** - dictionaries: WebTransportConnectionStats.
- `Modules/webtransport/WebTransportDatagramDuplexStream.idl` **[B18]** - WebTransportDatagramDuplexStream: createWritable, readable, maxDatagramSize, incomingMaxAge, outgoingMaxAge, incomingMaxBufferedDatagrams, outgoingMaxBufferedDatagrams.
- `Modules/webtransport/WebTransportDatagramStats.idl` **[B18]** - dictionaries: WebTransportDatagramStats.
- `Modules/webtransport/WebTransportDatagramsWritable.idl` **[B18]** - WebTransportDatagramsWritable: sendGroup, sendOrder.
- `Modules/webtransport/WebTransportError.idl` **[B18]** - WebTransportError: constructor, source, streamErrorCode.
- `Modules/webtransport/WebTransportErrorOptions.idl` **[B18]** - dictionaries: WebTransportErrorOptions.
- `Modules/webtransport/WebTransportErrorSource.idl` **[B18]** - enums: WebTransportErrorSource.
- `Modules/webtransport/WebTransportHash.idl` **[B18]** - dictionaries: WebTransportHash.
- `Modules/webtransport/WebTransportOptions.idl` **[B18]** - dictionaries: WebTransportOptions.
- `Modules/webtransport/WebTransportReceiveStream.idl` **[B18]** - WebTransportReceiveStream: getStats.
- `Modules/webtransport/WebTransportReceiveStreamStats.idl` **[B18]** - dictionaries: WebTransportReceiveStreamStats.
- `Modules/webtransport/WebTransportReliabilityMode.idl` **[B18]** - enums: WebTransportReliabilityMode.
- `Modules/webtransport/WebTransportSendGroup.idl` **[B18]** - WebTransportSendGroup: getStats.
- `Modules/webtransport/WebTransportSendOptions.idl` **[B18]** - dictionaries: WebTransportSendOptions.
- `Modules/webtransport/WebTransportSendStream.idl` **[B18]** - WebTransportSendStream: sendGroup, sendOrder, getStats.
- `Modules/webtransport/WebTransportSendStreamOptions.idl` **[B18]** - dictionaries: WebTransportSendStreamOptions.
- `Modules/webtransport/WebTransportSendStreamStats.idl` **[B18]** - dictionaries: WebTransportSendStreamStats.
### Modules/webxr

- `Modules/webxr/Navigator+WebXR.idl` **[B15]** - Navigator (partial): xr.
- `Modules/webxr/WebXRBoundedReferenceSpace.idl` **[B15]** - WebXRBoundedReferenceSpace: boundsGeometry.
- `Modules/webxr/WebXRFrame+HandInput.idl` **[B15]** - WebXRFrame (partial): getJointPose, fillJointRadii, fillPoses.
- `Modules/webxr/WebXRFrame+HitTest.idl` **[B15]** - WebXRFrame (partial): getHitTestResults, getHitTestResultsForTransientInput.
- `Modules/webxr/WebXRFrame.idl` **[B15]** - WebXRFrame: session, getViewerPose, getPose.
- `Modules/webxr/WebXRHand.idl` **[B15]** - WebXRHand: size, get; collections: iterable<XRHandJoint, WebXRJointSpace?>;.
- `Modules/webxr/WebXRHitTestResult.idl` **[B15]** - WebXRHitTestResult: getPose.
- `Modules/webxr/WebXRHitTestSource.idl` **[B15]** - WebXRHitTestSource: cancel.
- `Modules/webxr/WebXRInputSource+Gamepad.idl` **[B15]** - WebXRInputSource (partial): gamepad.
- `Modules/webxr/WebXRInputSource+HandInput.idl` **[B15]** - WebXRInputSource (partial): hand.
- `Modules/webxr/WebXRInputSource.idl` **[B15]** - WebXRInputSource: handedness, targetRayMode, targetRaySpace, gripSpace, profiles.
- `Modules/webxr/WebXRInputSourceArray.idl` **[B15]** - WebXRInputSourceArray: length, <indexed operation>; collections: iterable<WebXRInputSource>;.
- `Modules/webxr/WebXRJointPose.idl` **[B15]** - WebXRJointPose: radius.
- `Modules/webxr/WebXRJointSpace.idl` **[B15]** - WebXRJointSpace: jointName.
- `Modules/webxr/WebXRLayer.idl` **[B15]** - WebXRLayer: no own parsed members.
- `Modules/webxr/WebXRPose.idl` **[B15]** - WebXRPose: transform, emulatedPosition.
- `Modules/webxr/WebXRRay.idl` **[B15]** - WebXRRay: constructor, origin, direction, matrix.
- `Modules/webxr/WebXRReferenceSpace.idl` **[B15]** - WebXRReferenceSpace: getOffsetReferenceSpace, onreset.
- `Modules/webxr/WebXRRenderState+Layers.idl` **[B15]** - WebXRRenderState (partial): layers.
- `Modules/webxr/WebXRRenderState.idl` **[B15]** - WebXRRenderState: depthNear, depthFar, passthroughFullyObscured, inlineVerticalFieldOfView, baseLayer.
- `Modules/webxr/WebXRRigidTransform.idl` **[B15]** - WebXRRigidTransform: constructor, position, orientation, matrix, inverse.
- `Modules/webxr/WebXRSession+AR.idl` **[B15]** - WebXRSession (partial): interactionMode, environmentBlendMode.
- `Modules/webxr/WebXRSession+HitTest.idl` **[B15]** - WebXRSession (partial): requestHitTestSource, requestHitTestSourceForTransientInput.
- `Modules/webxr/WebXRSession+Layers.idl` **[B15]** - WebXRSession (partial): maxRenderLayers.
- `Modules/webxr/WebXRSession.idl` **[B15]** - WebXRSession: visibilityState, renderState, inputSources, enabledFeatures, updateRenderState, requestReferenceSpace, requestAnimationFrame, cancelAnimationFrame, end, onend, onselect, oninputsourceschange, onselectstart, onselectend, onsqueeze, onsqueezestart, onsqueezeend, onvisibilitychange.
- `Modules/webxr/WebXRSpace.idl` **[B15]** - WebXRSpace: no own parsed members.
- `Modules/webxr/WebXRSystem.idl` **[B15]** - WebXRSystem: isSessionSupported, requestSession, ondevicechange.
- `Modules/webxr/WebXRTransientInputHitTestResult.idl` **[B15]** - WebXRTransientInputHitTestResult: inputSource, results.
- `Modules/webxr/WebXRTransientInputHitTestSource.idl` **[B15]** - WebXRTransientInputHitTestSource: cancel.
- `Modules/webxr/WebXRView.idl` **[B15]** - WebXRView: eye, projectionMatrix, transform, recommendedViewportScale, requestViewportScale.
- `Modules/webxr/WebXRViewerPose.idl` **[B15]** - WebXRViewerPose: views.
- `Modules/webxr/WebXRViewport.idl` **[B15]** - WebXRViewport: x, y, width, height.
- `Modules/webxr/WebXRWebGLLayer.idl` **[B15]** - WebXRWebGLLayer: constructor, antialias, ignoreDepthValues, framebuffer, framebufferWidth, framebufferHeight, getViewport, getNativeFramebufferScaleFactor.
- `Modules/webxr/XRCompositionLayer.idl` **[B15]** - XRCompositionLayer: layout, blendTextureSourceAlpha, forceMonoPresentation, opacity, mipLevels, quality, needsRedraw, destroy.
- `Modules/webxr/XRCubeLayer.idl` **[B15]** - XRCubeLayer: space, orientation, onredraw.
- `Modules/webxr/XRCubeLayerInit.idl` **[B15]** - dictionaries: XRCubeLayerInit.
- `Modules/webxr/XRCylinderLayer.idl` **[B15]** - XRCylinderLayer: space, transform, radius, centralAngle, aspectRatio, onredraw.
- `Modules/webxr/XRCylinderLayerInit.idl` **[B15]** - dictionaries: XRCylinderLayerInit.
- `Modules/webxr/XREnvironmentBlendMode.idl` **[B15]** - enums: XREnvironmentBlendMode.
- `Modules/webxr/XREquirectLayer.idl` **[B15]** - XREquirectLayer: space, transform, radius, centralHorizontalAngle, upperVerticalAngle, lowerVerticalAngle, onredraw.
- `Modules/webxr/XREquirectLayerInit.idl` **[B15]** - dictionaries: XREquirectLayerInit.
- `Modules/webxr/XREye.idl` **[B15]** - enums: XREye.
- `Modules/webxr/XRFrameRequestCallback.idl` **[B15]** - callbacks: XRFrameRequestCallback.
- `Modules/webxr/XRGPUBinding.idl` **[B15]** - XRGPUBinding: constructor, nativeProjectionScaleFactor, createProjectionLayer, getSubImage, getViewSubImage, getPreferredColorFormat.
- `Modules/webxr/XRGPULayerInit.idl` **[B15]** - dictionaries: XRGPULayerInit.
- `Modules/webxr/XRGPUProjectionLayerInit.idl` **[B15]** - dictionaries: XRGPUProjectionLayerInit.
- `Modules/webxr/XRGPUSubImage.idl` **[B15]** - XRGPUSubImage: colorTexture, depthStencilTexture, motionVectorTexture, getViewDescriptor.
- `Modules/webxr/XRHandedness.idl` **[B15]** - enums: XRHandedness.
- `Modules/webxr/XRHandJoint.idl` **[B15]** - enums: XRHandJoint.
- `Modules/webxr/XRHitTestOptionsInit.idl` **[B15]** - dictionaries: XRHitTestOptionsInit.
- `Modules/webxr/XRHitTestTrackableType.idl` **[B15]** - enums: XRHitTestTrackableType.
- `Modules/webxr/XRInputSourceEvent.idl` **[B15]** - XRInputSourceEvent: constructor, frame, inputSource; dictionaries: XRInputSourceEventInit.
- `Modules/webxr/XRInputSourcesChangeEvent.idl` **[B15]** - XRInputSourcesChangeEvent: constructor, session, added, removed; dictionaries: XRInputSourcesChangeEventInit.
- `Modules/webxr/XRInteractionMode.idl` **[B15]** - enums: XRInteractionMode.
- `Modules/webxr/XRLayerEvent.idl` **[B15]** - XRLayerEvent: constructor, layer; dictionaries: XRLayerEventInit.
- `Modules/webxr/XRLayerInit.idl` **[B15]** - dictionaries: XRLayerInit.
- `Modules/webxr/XRLayerLayout.idl` **[B15]** - enums: XRLayerLayout.
- `Modules/webxr/XRLayerQuality.idl` **[B15]** - enums: XRLayerQuality.
- `Modules/webxr/XRProjectionLayer.idl` **[B15]** - XRProjectionLayer: textureWidth, textureHeight, textureArrayLength, ignoreDepthValues, fixedFoveation, deltaPose.
- `Modules/webxr/XRProjectionLayerInit.idl` **[B15]** - dictionaries: XRProjectionLayerInit.
- `Modules/webxr/XRQuadLayer.idl` **[B15]** - XRQuadLayer: space, transform, width, height, onredraw.
- `Modules/webxr/XRQuadLayerInit.idl` **[B15]** - dictionaries: XRQuadLayerInit.
- `Modules/webxr/XRRayDirectionInit.idl` **[B15]** - dictionaries: XRRayDirectionInit.
- `Modules/webxr/XRReferenceSpaceEvent.idl` **[B15]** - XRReferenceSpaceEvent: constructor, referenceSpace, transform; dictionaries: XRReferenceSpaceEventInit.
- `Modules/webxr/XRReferenceSpaceType.idl` **[B15]** - enums: XRReferenceSpaceType.
- `Modules/webxr/XRRenderStateInit.idl` **[B15]** - dictionaries: XRRenderStateInit.
- `Modules/webxr/XRSessionEvent.idl` **[B15]** - XRSessionEvent: constructor, session; dictionaries: XRSessionEventInit.
- `Modules/webxr/XRSessionInit.idl` **[B15]** - dictionaries: XRSessionInit.
- `Modules/webxr/XRSessionMode.idl` **[B15]** - enums: XRSessionMode.
- `Modules/webxr/XRSubImage.idl` **[B15]** - XRSubImage: viewport.
- `Modules/webxr/XRTargetRayMode.idl` **[B15]** - enums: XRTargetRayMode.
- `Modules/webxr/XRTextureType.idl` **[B15]** - enums: XRTextureType.
- `Modules/webxr/XRTransientInputHitTestOptionsInit.idl` **[B15]** - dictionaries: XRTransientInputHitTestOptionsInit.
- `Modules/webxr/XRVisibilityState.idl` **[B15]** - enums: XRVisibilityState.
- `Modules/webxr/XRWebGLBinding.idl` **[B15]** - XRWebGLBinding: constructor, nativeProjectionScaleFactor, usesDepthValues, createProjectionLayer, createQuadLayer, createCylinderLayer, createEquirectLayer, createCubeLayer, getSubImage, getViewSubImage.
- `Modules/webxr/XRWebGLLayerInit.idl` **[B15]** - dictionaries: XRWebGLLayerInit.
- `Modules/webxr/XRWebGLSubImage.idl` **[B15]** - XRWebGLSubImage: colorTexture, depthStencilTexture, motionVectorTexture, imageIndex, colorTextureWidth, colorTextureHeight, depthStencilTextureWidth, depthStencilTextureHeight, motionVectorTextureWidth, motionVectorTextureHeight.
### page

- `page/BarProp.idl` **[B23]** - BarProp: visible.
- `page/Crypto.idl` **[B22]** - Crypto: subtle, getRandomValues, randomUUID.
- `page/csp/CSPViolationReportBody.idl` **[H10, B21]** - CSPViolationReportBody: documentURL, referrer, blockedURL, effectiveDirective, originalPolicy, sourceFile, sample, disposition, statusCode, lineNumber, columnNumber, toJSON.
- `page/DOMSelection.idl` **[G09, T04]** - DOMSelection: anchorNode, anchorOffset, focusNode, focusOffset, isCollapsed, rangeCount, type, direction, getRangeAt, addRange, removeRange, removeAllRanges, getComposedRanges, empty, collapse, setPosition, collapseToStart, collapseToEnd, extend, setBaseAndExtent, selectAllChildren, deleteFromDocument, containsNode, toString, modify, baseNode, baseOffset, extentNode, extentOffset.
- `page/DOMWindow+Compat.idl` **[B23]** - DOMWindow (partial): orientation, onorientationchange.
- `page/DOMWindow+CSSOM.idl` **[C01, C02]** - DOMWindow (partial): getComputedStyle.
- `page/DOMWindow+CSSOMView.idl` **[G01, G02, G04, G05, B13]** - DOMWindow (partial): matchMedia, screen, moveTo, moveBy, resizeTo, resizeBy, innerWidth, innerHeight, scrollX, pageXOffset, scrollY, pageYOffset, scroll, scrollTo, scrollBy, screenX, screenLeft, screenY, screenTop, outerWidth, outerHeight, devicePixelRatio, styleMedia.
- `page/DOMWindow+DeviceMotion.idl` **[B12]** - DOMWindow (partial): ondevicemotion.
- `page/DOMWindow+DeviceOrientation.idl` **[B12]** - DOMWindow (partial): ondeviceorientation.
- `page/DOMWindow+RequestIdleCallback.idl` **[H01]** - DOMWindow (partial): requestIdleCallback, cancelIdleCallback.
- `page/DOMWindow+Selection.idl` **[G09, T04]** - DOMWindow (partial): getSelection.
- `page/DOMWindow+VisualViewport.idl` **[G03]** - DOMWindow (partial): visualViewport.
- `page/DOMWindow.idl` **[B13, B20, B23, G14, H08]** - DOMWindow: close, closed, focus, blur, postMessage, location, window, self, opener, frames, length, top, parent, document, originAgentCluster, name, history, navigation, customElements, locationbar, menubar, personalbar, scrollbars, statusbar, toolbar, status, stop, frameElement, open, navigator, clientInformation, alert, confirm, prompt, print, captureEvents, releaseEvents, event, find, offscreenBuffering, webkitRequestAnimationFrame, webkitCancelAnimationFrame, webkitCancelRequestAnimationFrame, ongesturechange, ongestureend, ongesturestart, getMatchedCSSRules, webkitConvertPointFromPageToNode, webkitConvertPointFromNodeToPage, collectMatchingElementsInFlatTree, matchingElementInFlatTree; includes: DOMWindow -> AnimationFrameProvider, DOMWindow -> GlobalEventHandlers, DOMWindow -> WindowEventHandlers, DOMWindow -> WindowOrWorkerGlobalScope, DOMWindow -> WindowLocalStorage, DOMWindow -> WindowPush, DOMWindow -> WindowSessionStorage.
- `page/EventCounts.idl` **[H02, H03, H04]** - EventCounts: no own parsed members; collections: readonly maplike<DOMString, unsigned long long>;.
- `page/EventSource.idl` **[B18]** - EventSource: constructor, url, withCredentials, readyState, onopen, onmessage, onerror, close; dictionaries: EventSourceInit.
- `page/FragmentDirective.idl` **[B20]** - FragmentDirective: no own parsed members.
- `page/GetComposedRangesOptions.idl` **[G09, T04]** - dictionaries: GetComposedRangesOptions.
- `page/History.idl` **[B20]** - History: length, scrollRestoration, state, back, forward, go, pushState, replaceState; enums: ScrollRestoration.
- `page/IntersectionObserver.idl` **[G11]** - IntersectionObserver: constructor, root, rootMargin, scrollMargin, thresholds, observe, unobserve, disconnect, takeRecords; dictionaries: IntersectionObserverInit.
- `page/IntersectionObserverCallback.idl` **[G11]** - callbacks: IntersectionObserverCallback.
- `page/IntersectionObserverEntry.idl` **[G11]** - IntersectionObserverEntry: constructor, time, rootBounds, boundingClientRect, intersectionRect, isIntersecting, intersectionRatio, target; dictionaries: IntersectionObserverEntryInit.
- `page/IsLoggedIn.idl` **[B23]** - enums: IsLoggedIn.
- `page/LargestContentfulPaint.idl` **[H02, H03, H04]** - LargestContentfulPaint: loadTime, renderTime, size, id, url, element, toJSON; includes: LargestContentfulPaint -> PaintTimingMixin.
- `page/Location.idl` **[B20]** - Location: href, assign, replace, reload, protocol, host, hostname, port, pathname, search, hash, origin, ancestorOrigins.
- `page/NavigateEvent.idl` **[B20]** - NavigateEvent: constructor, navigationType, destination, canIntercept, userInitiated, hashChange, signal, formData, downloadRequest, info, sourceElement, hasUAVisualTransition, intercept, scroll; dictionaries: NavigateEventInit, NavigationInterceptOptions; enums: NavigationFocusReset, NavigationScrollBehavior.
- `page/Navigation.idl` **[B20]** - Navigation: entries, currentEntry, updateCurrentEntry, transition, activation, canGoBack, canGoForward, navigate, reload, traverseTo, back, forward, onnavigate, onnavigatesuccess, onnavigateerror, oncurrententrychange; dictionaries: NavigationNavigateOptions, NavigationOptions, NavigationReloadOptions, NavigationResult, NavigationUpdateCurrentEntryOptions; enums: NavigationHistoryBehavior.
- `page/NavigationActivation.idl` **[B20]** - NavigationActivation: from, entry, navigationType.
- `page/NavigationCurrentEntryChangeEvent.idl` **[B20]** - NavigationCurrentEntryChangeEvent: constructor, navigationType, from; dictionaries: NavigationCurrentEntryChangeEventInit.
- `page/NavigationDestination.idl` **[B20]** - NavigationDestination: url, key, id, index, sameDocument, getState.
- `page/NavigationHistoryEntry.idl` **[B20]** - NavigationHistoryEntry: url, key, id, index, sameDocument, getState, ondispose.
- `page/NavigationInterceptHandler.idl` **[B20]** - callbacks: NavigationInterceptHandler.
- `page/NavigationNavigationType.idl` **[B20]** - enums: NavigationNavigationType.
- `page/NavigationTransition.idl` **[B20]** - NavigationTransition: navigationType, from, finished.
- `page/Navigator+LoginStatus.idl` **[B23]** - Navigator (partial): setStatus, isLoggedIn.
- `page/Navigator+UserActivation.idl` **[I12]** - Navigator (partial): userActivation.
- `page/Navigator.idl` **[I12, B17, B23]** - Navigator: standalone, hardwareConcurrency; includes: Navigator -> NavigatorBadge, Navigator -> NavigatorID, Navigator -> NavigatorLanguage, Navigator -> NavigatorLocks, Navigator -> NavigatorMaxTouchPoints, Navigator -> NavigatorOnLine, Navigator -> NavigatorCookies, Navigator -> NavigatorPlugins, Navigator -> NavigatorServiceWorker, Navigator -> NavigatorShare, Navigator -> NavigatorStorage, Navigator -> NavigatorGPU, Navigator -> NavigatorGlobalPrivacyControl, Navigator -> NavigatorUA.
- `page/NavigatorCookies.idl` **[B19]** - NavigatorCookies (mixin): cookieEnabled.
- `page/NavigatorGlobalPrivacyControl.idl` **[B23]** - NavigatorGlobalPrivacyControl (mixin): globalPrivacyControl.
- `page/NavigatorID.idl` **[B23]** - NavigatorID (mixin): appCodeName, appName, appVersion, platform, product, productSub, userAgent, vendor, vendorSub.
- `page/NavigatorLanguage.idl` **[H06]** - NavigatorLanguage (mixin): language, languages.
- `page/NavigatorOnLine.idl` **[B18]** - NavigatorOnLine (mixin): onLine.
- `page/NavigatorPlugins.idl` **[B23]** - NavigatorPlugins (mixin): plugins, mimeTypes, javaEnabled, pdfViewerEnabled.
- `page/NavigatorServiceWorker.idl` **[I12, B17, B23]** - NavigatorServiceWorker (mixin): serviceWorker.
- `page/NavigatorShare.idl` **[B14]** - NavigatorShare (mixin): canShare, share.
- `page/NavigatorStorage.idl` **[B19]** - NavigatorStorage (mixin): storage.
- `page/NavigatorUA.idl` **[B23]** - NavigatorUA (mixin): userAgentData.
- `page/NavigatorUABrandVersion.idl` **[B23]** - dictionaries: NavigatorUABrandVersion.
- `page/NavigatorUAData.idl` **[B23]** - NavigatorUAData: brands, mobile, platform, getHighEntropyValues, toJSON.
- `page/PaintTimingMixin.idl` **[H02, H03, H04]** - PaintTimingMixin (mixin): paintTime, presentationTime.
- `page/Performance+EventCounts.idl` **[H02, H03, H04]** - Performance (partial): eventCounts, interactionCount.
- `page/Performance+NavigationTiming.idl` **[H02, H03, H04]** - Performance (partial): navigation, timing.
- `page/Performance+PerformanceTimeline.idl` **[H02, H03, H04]** - Performance (partial): getEntries, getEntriesByType, getEntriesByName.
- `page/Performance+ResourceTiming.idl` **[H02, H03, H04]** - Performance (partial): clearResourceTimings, setResourceTimingBufferSize, onresourcetimingbufferfull.
- `page/Performance+UserTiming.idl` **[H02, H03, H04]** - Performance (partial): mark, clearMarks, measure, clearMeasures.
- `page/Performance.idl` **[H02, H03, H04]** - Performance: now, timeOrigin, toJSON.
- `page/PerformanceEntry.idl` **[H02, H03, H04]** - PerformanceEntry: name, entryType, startTime, duration, toJSON.
- `page/PerformanceEventTiming.idl` **[H02, H03, H04]** - PerformanceEventTiming: processingStart, processingEnd, cancelable, target, interactionId, toJSON.
- `page/PerformanceMark.idl` **[H02, H03, H04]** - PerformanceMark: constructor, detail.
- `page/PerformanceMarkOptions.idl` **[H02, H03, H04]** - dictionaries: PerformanceMarkOptions.
- `page/PerformanceMeasure.idl` **[H02, H03, H04]** - PerformanceMeasure: detail.
- `page/PerformanceMeasureOptions.idl` **[H02, H03, H04]** - dictionaries: PerformanceMeasureOptions.
- `page/PerformanceNavigation.idl` **[H02, H03, H04]** - PerformanceNavigation: type, redirectCount, toJSON.
- `page/PerformanceNavigationTiming.idl` **[H02, H03, H04]** - PerformanceNavigationTiming: unloadEventStart, unloadEventEnd, domInteractive, domContentLoadedEventStart, domContentLoadedEventEnd, domComplete, loadEventStart, loadEventEnd, type, redirectCount, toJSON; enums: NavigationType.
- `page/PerformanceObserver.idl` **[H02, H03, H04]** - PerformanceObserver: constructor, observe, disconnect, takeRecords, supportedEntryTypes; dictionaries: PerformanceObserverInit.
- `page/PerformanceObserverCallback.idl` **[H02, H03, H04]** - callbacks: PerformanceObserverCallback.
- `page/PerformanceObserverEntryList.idl` **[H02, H03, H04]** - PerformanceObserverEntryList: getEntries, getEntriesByType, getEntriesByName.
- `page/PerformancePaintTiming.idl` **[H02, H03, H04]** - PerformancePaintTiming: no own parsed members.
- `page/PerformanceResourceTiming.idl` **[H02, H03, H04]** - PerformanceResourceTiming: initiatorType, nextHopProtocol, workerStart, redirectStart, redirectEnd, fetchStart, domainLookupStart, domainLookupEnd, connectStart, connectEnd, secureConnectionStart, requestStart, finalResponseHeadersStart, firstInterimResponseStart, responseStart, responseEnd, transferSize, encodedBodySize, decodedBodySize, workerRouterEvaluationStart, workerCacheLookupStart, workerMatchedRouterSource, workerFinalRouterSource, deliveryType, serverTiming, toJSON.
- `page/PerformanceServerTiming.idl` **[H02, H03, H04]** - PerformanceServerTiming: name, duration, description, toJSON.
- `page/PerformanceTiming.idl` **[H02, H03, H04]** - PerformanceTiming: navigationStart, unloadEventStart, unloadEventEnd, redirectStart, redirectEnd, fetchStart, domainLookupStart, domainLookupEnd, connectStart, connectEnd, secureConnectionStart, requestStart, responseStart, responseEnd, domLoading, domInteractive, domContentLoadedEventStart, domContentLoadedEventEnd, domComplete, loadEventStart, loadEventEnd, toJSON.
- `page/ResizeObserver.idl` **[G10]** - ResizeObserver: constructor, observe, unobserve, disconnect.
- `page/ResizeObserverBoxOptions.idl` **[G10]** - enums: ResizeObserverBoxOptions.
- `page/ResizeObserverCallback.idl` **[G10]** - callbacks: ResizeObserverCallback.
- `page/ResizeObserverEntry.idl` **[G10]** - ResizeObserverEntry: target, contentRect, borderBoxSize, contentBoxSize.
- `page/ResizeObserverOptions.idl` **[G10]** - dictionaries: ResizeObserverOptions.
- `page/ResizeObserverSize.idl` **[G10]** - ResizeObserverSize: inlineSize, blockSize.
- `page/Screen.idl` **[G04]** - Screen: height, width, colorDepth, pixelDepth, availLeft, availTop, availHeight, availWidth, orientation.
- `page/ScreenOrientation.idl` **[G04]** - ScreenOrientation: lock, unlock, type, angle, onchange; enums: OrientationLockType, OrientationType.
- `page/ScrollBehavior.idl` **[G05]** - enums: ScrollBehavior.
- `page/ScrollIntoViewOptions.idl` **[G05]** - dictionaries: ScrollIntoViewOptions.
- `page/ScrollLogicalPosition.idl` **[G05]** - enums: ScrollLogicalPosition.
- `page/ScrollOptions.idl` **[G05]** - dictionaries: ScrollOptions.
- `page/ScrollToOptions.idl` **[G05]** - dictionaries: ScrollToOptions.
- `page/ShadowRealmGlobalScope.idl` **[B23]** - ShadowRealmGlobalScope: self.
- `page/ShareData.idl` **[B14]** - dictionaries: ShareData.
- `page/StructuredSerializeOptions.idl` **[H08, H14]** - dictionaries: StructuredSerializeOptions.
- `page/UADataValues.idl` **[B23]** - dictionaries: UADataValues.
- `page/UALowEntropyJSON.idl` **[B23]** - dictionaries: UALowEntropyJSON.
- `page/UndoItem.idl` **[T05]** - UndoItem: constructor, label; dictionaries: UndoItemInit.
- `page/UndoManager.idl` **[T05]** - UndoManager: addItem.
- `page/UserMessageHandler.idl` **[H08, H14]** - UserMessageHandler: postMessage, postLegacySynchronousMessage.
- `page/UserMessageHandlersNamespace.idl` **[H08, H14]** - UserMessageHandlersNamespace: <indexed operation>.
- `page/VisualViewport.idl` **[G03]** - VisualViewport: offsetLeft, offsetTop, pageLeft, pageTop, width, height, scale, onresize, onscroll.
- `page/WebKitBuffer.idl` **[H08, H14]** - WebKitBuffer: asUTF16String, asLatin1String.
- `page/WebKitBufferNamespace.idl` **[H08, H14]** - WebKitBufferNamespace: <indexed operation>.
- `page/WebKitJSHandle.idl` **[H08, H14]** - WebKitJSHandle: no own parsed members.
- `page/WebKitNamespace.idl` **[H08, H14]** - WebKitNamespace: messageHandlers, buffers, evaluateScript, createJSHandle, createNodeSnapshot; dictionaries: WebKitNodeSnapshotInit.
- `page/WebKitNodeSnapshot.idl` **[H08, H14]** - WebKitNodeSnapshot: no own parsed members.
- `page/WebKitPoint.idl` **[H08, H14]** - WebKitPoint: constructor, x, y.
- `page/WindowEventHandlers.idl` **[B20]** - WindowEventHandlers (mixin): onafterprint, onbeforeprint, onbeforeunload, onhashchange, onlanguagechange, onmessage, onmessageerror, onoffline, ononline, onpagehide, onpagereveal, onpageshow, onpageswap, onpopstate, onrejectionhandled, onstorage, onunhandledrejection, onunload.
- `page/WindowLocalStorage.idl` **[B19]** - WindowLocalStorage (mixin): localStorage.
- `page/WindowOrWorkerGlobalScope+Crypto.idl` **[B22]** - WindowOrWorkerGlobalScope (partial) (mixin): crypto.
- `page/WindowOrWorkerGlobalScope+Performance.idl` **[H02, H03, H04]** - WindowOrWorkerGlobalScope (partial) (mixin): performance.
- `page/WindowOrWorkerGlobalScope.idl` **[A12, H12, H14, B21]** - WindowOrWorkerGlobalScope (mixin): origin, isSecureContext, crossOriginIsolated, reportError, atob, btoa, setTimeout, clearTimeout, setInterval, clearInterval, queueMicrotask, createImageBitmap, structuredClone.
- `page/WindowPostMessageOptions.idl` **[H08, H14]** - dictionaries: WindowPostMessageOptions.
- `page/WindowSessionStorage.idl` **[B19]** - WindowSessionStorage (mixin): sessionStorage.
- `page/WorkerNavigator.idl` **[I12, B17, B23]** - WorkerNavigator: hardwareConcurrency; includes: WorkerNavigator -> NavigatorBadge, WorkerNavigator -> NavigatorID, WorkerNavigator -> NavigatorLanguage, WorkerNavigator -> NavigatorLocks, WorkerNavigator -> NavigatorOnLine, WorkerNavigator -> NavigatorServiceWorker, WorkerNavigator -> NavigatorStorage, WorkerNavigator -> NavigatorGPU, WorkerNavigator -> NavigatorUA, WorkerNavigator -> NavigatorGlobalPrivacyControl.
### platform

- `platform/sql/stub/Database.idl` **[B19]** - Database: no own parsed members.
### plugins

- `plugins/DOMMimeType.idl` **[B23]** - DOMMimeType: type, suffixes, description, enabledPlugin.
- `plugins/DOMMimeTypeArray.idl` **[B23]** - DOMMimeTypeArray: length, item, namedItem.
- `plugins/DOMPlugin.idl` **[B23]** - DOMPlugin: name, filename, description, length, item, namedItem.
- `plugins/DOMPluginArray.idl` **[B23]** - DOMPluginArray: length, item, namedItem, refresh.
### storage

- `storage/Storage.idl` **[B19]** - Storage: length, key, getItem, setItem, removeItem, clear.
- `storage/StorageEvent.idl` **[B19]** - StorageEvent: constructor, key, oldValue, newValue, url, storageArea, initStorageEvent; dictionaries: StorageEventInit.
### svg

- `svg/Document+SVG.idl` **[D09, S11]** - Document (partial): rootElement.
- `svg/SVGAElement.idl` **[D09, S11]** - SVGAElement: target, hreflang, rel, relList, type; includes: SVGAElement -> SVGURIReference.
- `svg/SVGAngle.idl` **[S04]** - SVGAngle: unitType, value, valueInSpecifiedUnits, valueAsString, newValueSpecifiedUnits, convertToSpecifiedUnits.
- `svg/SVGAnimatedAngle.idl` **[S04, S07]** - SVGAnimatedAngle: baseVal, animVal.
- `svg/SVGAnimatedBoolean.idl` **[S04, S07]** - SVGAnimatedBoolean: baseVal, animVal.
- `svg/SVGAnimatedEnumeration.idl` **[S04, S07]** - SVGAnimatedEnumeration: baseVal, animVal.
- `svg/SVGAnimatedInteger.idl` **[S04, S07]** - SVGAnimatedInteger: baseVal, animVal.
- `svg/SVGAnimatedLength.idl` **[S04, S07]** - SVGAnimatedLength: baseVal, animVal.
- `svg/SVGAnimatedLengthList.idl` **[S04, S07]** - SVGAnimatedLengthList: baseVal, animVal.
- `svg/SVGAnimatedNumber.idl` **[S04, S07]** - SVGAnimatedNumber: baseVal, animVal.
- `svg/SVGAnimatedNumberList.idl` **[S04, S07]** - SVGAnimatedNumberList: baseVal, animVal.
- `svg/SVGAnimatedPreserveAspectRatio.idl` **[S04, S07]** - SVGAnimatedPreserveAspectRatio: baseVal, animVal.
- `svg/SVGAnimatedRect.idl` **[S04, S07]** - SVGAnimatedRect: baseVal, animVal.
- `svg/SVGAnimatedString.idl` **[S04, S07]** - SVGAnimatedString: baseVal, animVal.
- `svg/SVGAnimatedTransformList.idl` **[S04, S07]** - SVGAnimatedTransformList: baseVal, animVal.
- `svg/SVGAnimateElement.idl` **[S04, S07]** - SVGAnimateElement: no own parsed members.
- `svg/SVGAnimateMotionElement.idl` **[S04, S07]** - SVGAnimateMotionElement: no own parsed members.
- `svg/SVGAnimateTransformElement.idl` **[S04, S07]** - SVGAnimateTransformElement: no own parsed members.
- `svg/SVGAnimationElement.idl` **[S04, S07]** - SVGAnimationElement: targetElement, getStartTime, getCurrentTime, getSimpleDuration, beginElement, beginElementAt, endElement, endElementAt, onbegin, onend, onrepeat; includes: SVGAnimationElement -> SVGTests.
- `svg/SVGCircleElement.idl` **[S03]** - SVGCircleElement: cx, cy, r.
- `svg/SVGClipPathElement.idl` **[S10]** - SVGClipPathElement: clipPathUnits, transform.
- `svg/SVGComponentTransferFunctionElement.idl` **[D09, S11]** - SVGComponentTransferFunctionElement: type, tableValues, slope, intercept, amplitude, exponent, offset.
- `svg/SVGDefsElement.idl` **[D09, S11]** - SVGDefsElement: no own parsed members.
- `svg/SVGDescElement.idl` **[D09, S11]** - SVGDescElement: no own parsed members.
- `svg/SVGElement.idl` **[D09, S11]** - SVGElement: className, ownerSVGElement, viewportElement; includes: SVGElement -> GlobalEventHandlers, SVGElement -> DocumentAndElementEventHandlers, SVGElement -> HTMLOrSVGOrMathMLElement, SVGElement -> ElementCSSInlineStyle.
- `svg/SVGEllipseElement.idl` **[S03]** - SVGEllipseElement: cx, cy, rx, ry.
- `svg/SVGFEBlendElement.idl` **[S09]** - SVGFEBlendElement: in1, in2, mode; includes: SVGFEBlendElement -> SVGFilterPrimitiveStandardAttributes.
- `svg/SVGFEColorMatrixElement.idl` **[S09]** - SVGFEColorMatrixElement: in1, type, values; includes: SVGFEColorMatrixElement -> SVGFilterPrimitiveStandardAttributes.
- `svg/SVGFEComponentTransferElement.idl` **[S09]** - SVGFEComponentTransferElement: in1; includes: SVGFEComponentTransferElement -> SVGFilterPrimitiveStandardAttributes.
- `svg/SVGFECompositeElement.idl` **[S09]** - SVGFECompositeElement: in1, in2, operator, k1, k2, k3, k4; includes: SVGFECompositeElement -> SVGFilterPrimitiveStandardAttributes.
- `svg/SVGFEConvolveMatrixElement.idl` **[S09]** - SVGFEConvolveMatrixElement: in1, orderX, orderY, kernelMatrix, divisor, bias, targetX, targetY, edgeMode, kernelUnitLengthX, kernelUnitLengthY, preserveAlpha; includes: SVGFEConvolveMatrixElement -> SVGFilterPrimitiveStandardAttributes.
- `svg/SVGFEDiffuseLightingElement.idl` **[S09]** - SVGFEDiffuseLightingElement: in1, surfaceScale, diffuseConstant, kernelUnitLengthX, kernelUnitLengthY; includes: SVGFEDiffuseLightingElement -> SVGFilterPrimitiveStandardAttributes.
- `svg/SVGFEDisplacementMapElement.idl` **[S09]** - SVGFEDisplacementMapElement: in1, in2, scale, xChannelSelector, yChannelSelector; includes: SVGFEDisplacementMapElement -> SVGFilterPrimitiveStandardAttributes.
- `svg/SVGFEDistantLightElement.idl` **[S09]** - SVGFEDistantLightElement: azimuth, elevation.
- `svg/SVGFEDropShadowElement.idl` **[S09]** - SVGFEDropShadowElement: in1, dx, dy, stdDeviationX, stdDeviationY, setStdDeviation; includes: SVGFEDropShadowElement -> SVGFilterPrimitiveStandardAttributes.
- `svg/SVGFEFloodElement.idl` **[S09]** - SVGFEFloodElement: no own parsed members; includes: SVGFEFloodElement -> SVGFilterPrimitiveStandardAttributes.
- `svg/SVGFEFuncAElement.idl` **[S09]** - SVGFEFuncAElement: no own parsed members.
- `svg/SVGFEFuncBElement.idl` **[S09]** - SVGFEFuncBElement: no own parsed members.
- `svg/SVGFEFuncGElement.idl` **[S09]** - SVGFEFuncGElement: no own parsed members.
- `svg/SVGFEFuncRElement.idl` **[S09]** - SVGFEFuncRElement: no own parsed members.
- `svg/SVGFEGaussianBlurElement.idl` **[S09]** - SVGFEGaussianBlurElement: in1, stdDeviationX, stdDeviationY, edgeMode, setStdDeviation; includes: SVGFEGaussianBlurElement -> SVGFilterPrimitiveStandardAttributes.
- `svg/SVGFEImageElement.idl` **[S09]** - SVGFEImageElement: preserveAspectRatio; includes: SVGFEImageElement -> SVGFilterPrimitiveStandardAttributes, SVGFEImageElement -> SVGURIReference.
- `svg/SVGFEMergeElement.idl` **[S09]** - SVGFEMergeElement: no own parsed members; includes: SVGFEMergeElement -> SVGFilterPrimitiveStandardAttributes.
- `svg/SVGFEMergeNodeElement.idl` **[S09]** - SVGFEMergeNodeElement: in1.
- `svg/SVGFEMorphologyElement.idl` **[S09]** - SVGFEMorphologyElement: in1, operator, radiusX, radiusY; includes: SVGFEMorphologyElement -> SVGFilterPrimitiveStandardAttributes.
- `svg/SVGFEOffsetElement.idl` **[S09]** - SVGFEOffsetElement: in1, dx, dy; includes: SVGFEOffsetElement -> SVGFilterPrimitiveStandardAttributes.
- `svg/SVGFEPointLightElement.idl` **[S09]** - SVGFEPointLightElement: x, y, z.
- `svg/SVGFESpecularLightingElement.idl` **[S09]** - SVGFESpecularLightingElement: in1, surfaceScale, specularConstant, specularExponent, kernelUnitLengthX, kernelUnitLengthY; includes: SVGFESpecularLightingElement -> SVGFilterPrimitiveStandardAttributes.
- `svg/SVGFESpotLightElement.idl` **[S09]** - SVGFESpotLightElement: x, y, z, pointsAtX, pointsAtY, pointsAtZ, specularExponent, limitingConeAngle.
- `svg/SVGFETileElement.idl` **[S09]** - SVGFETileElement: in1; includes: SVGFETileElement -> SVGFilterPrimitiveStandardAttributes.
- `svg/SVGFETurbulenceElement.idl` **[S09]** - SVGFETurbulenceElement: baseFrequencyX, baseFrequencyY, numOctaves, seed, stitchTiles, type; includes: SVGFETurbulenceElement -> SVGFilterPrimitiveStandardAttributes.
- `svg/SVGFilterElement.idl` **[S09]** - SVGFilterElement: filterUnits, primitiveUnits, x, y, width, height; includes: SVGFilterElement -> SVGURIReference.
- `svg/SVGFilterPrimitiveStandardAttributes.idl` **[S09]** - SVGFilterPrimitiveStandardAttributes (mixin): x, y, width, height, result.
- `svg/SVGFitToViewBox.idl` **[S06]** - SVGFitToViewBox (mixin): viewBox, preserveAspectRatio.
- `svg/SVGFontElement.idl` **[S12]** - SVGFontElement: no own parsed members.
- `svg/SVGForeignObjectElement.idl` **[D09, S11]** - SVGForeignObjectElement: x, y, width, height.
- `svg/SVGGElement.idl` **[D09, S11]** - SVGGElement: no own parsed members.
- `svg/SVGGeometryElement.idl` **[S02]** - SVGGeometryElement: pathLength, isPointInFill, isPointInStroke, getTotalLength, getPointAtLength.
- `svg/SVGGradientElement.idl` **[S08]** - SVGGradientElement: gradientUnits, gradientTransform, spreadMethod; includes: SVGGradientElement -> SVGURIReference.
- `svg/SVGGraphicsElement.idl` **[S01]** - SVGGraphicsElement: transform, getBBox, getCTM, getScreenCTM; includes: SVGGraphicsElement -> SVGTests.
- `svg/SVGImageElement.idl` **[D09, S11]** - SVGImageElement: x, y, width, height, preserveAspectRatio, crossOrigin, decode; includes: SVGImageElement -> SVGURIReference.
- `svg/SVGLength.idl` **[S04]** - SVGLength: unitType, value, valueInSpecifiedUnits, valueAsString, newValueSpecifiedUnits, convertToSpecifiedUnits.
- `svg/SVGLengthList.idl` **[S04]** - SVGLengthList: length, numberOfItems, clear, initialize, getItem, insertItemBefore, replaceItem, removeItem, appendItem, <indexed operation>.
- `svg/SVGLinearGradientElement.idl` **[S08]** - SVGLinearGradientElement: x1, y1, x2, y2.
- `svg/SVGLineElement.idl` **[S03]** - SVGLineElement: x1, y1, x2, y2.
- `svg/SVGMarkerElement.idl` **[S10]** - SVGMarkerElement: refX, refY, markerUnits, markerWidth, markerHeight, orientType, orientAngle, orient, setOrientToAuto, setOrientToAngle; includes: SVGMarkerElement -> SVGFitToViewBox.
- `svg/SVGMaskElement.idl` **[S10]** - SVGMaskElement: maskUnits, maskContentUnits, x, y, width, height; includes: SVGMaskElement -> SVGTests.
- `svg/SVGMatrix.idl` **[S04]** - SVGMatrix: a, b, c, d, e, f, multiply, inverse, translate, scale, scaleNonUniform, rotate, rotateFromVector, flipX, flipY, skewX, skewY.
- `svg/SVGMetadataElement.idl` **[D09, S11]** - SVGMetadataElement: no own parsed members.
- `svg/SVGMPathElement.idl` **[S04, S07]** - SVGMPathElement: no own parsed members; includes: SVGMPathElement -> SVGURIReference.
- `svg/SVGNumber.idl` **[S04]** - SVGNumber: value.
- `svg/SVGNumberList.idl` **[S04]** - SVGNumberList: length, numberOfItems, clear, initialize, getItem, insertItemBefore, replaceItem, removeItem, appendItem, <indexed operation>.
- `svg/SVGPathElement.idl` **[S03]** - SVGPathElement: getPathSegAtLength, createSVGPathSegClosePath, createSVGPathSegMovetoAbs, createSVGPathSegMovetoRel, createSVGPathSegLinetoAbs, createSVGPathSegLinetoRel, createSVGPathSegCurvetoCubicAbs, createSVGPathSegCurvetoCubicRel, createSVGPathSegCurvetoQuadraticAbs, createSVGPathSegCurvetoQuadraticRel, createSVGPathSegArcAbs, createSVGPathSegArcRel, createSVGPathSegLinetoHorizontalAbs, createSVGPathSegLinetoHorizontalRel, createSVGPathSegLinetoVerticalAbs, createSVGPathSegLinetoVerticalRel, createSVGPathSegCurvetoCubicSmoothAbs, createSVGPathSegCurvetoCubicSmoothRel, createSVGPathSegCurvetoQuadraticSmoothAbs, createSVGPathSegCurvetoQuadraticSmoothRel, pathSegList, animatedPathSegList.
- `svg/SVGPathSeg.idl` **[S12]** - SVGPathSeg: pathSegType, pathSegTypeAsLetter.
- `svg/SVGPathSegArcAbs.idl` **[S12]** - SVGPathSegArcAbs: x, y, r1, r2, angle, largeArcFlag, sweepFlag.
- `svg/SVGPathSegArcRel.idl` **[S12]** - SVGPathSegArcRel: x, y, r1, r2, angle, largeArcFlag, sweepFlag.
- `svg/SVGPathSegClosePath.idl` **[S12]** - SVGPathSegClosePath: no own parsed members.
- `svg/SVGPathSegCurvetoCubicAbs.idl` **[S12]** - SVGPathSegCurvetoCubicAbs: x, y, x1, y1, x2, y2.
- `svg/SVGPathSegCurvetoCubicRel.idl` **[S12]** - SVGPathSegCurvetoCubicRel: x, y, x1, y1, x2, y2.
- `svg/SVGPathSegCurvetoCubicSmoothAbs.idl` **[S12]** - SVGPathSegCurvetoCubicSmoothAbs: x, y, x2, y2.
- `svg/SVGPathSegCurvetoCubicSmoothRel.idl` **[S12]** - SVGPathSegCurvetoCubicSmoothRel: x, y, x2, y2.
- `svg/SVGPathSegCurvetoQuadraticAbs.idl` **[S12]** - SVGPathSegCurvetoQuadraticAbs: x, y, x1, y1.
- `svg/SVGPathSegCurvetoQuadraticRel.idl` **[S12]** - SVGPathSegCurvetoQuadraticRel: x, y, x1, y1.
- `svg/SVGPathSegCurvetoQuadraticSmoothAbs.idl` **[S12]** - SVGPathSegCurvetoQuadraticSmoothAbs: x, y.
- `svg/SVGPathSegCurvetoQuadraticSmoothRel.idl` **[S12]** - SVGPathSegCurvetoQuadraticSmoothRel: x, y.
- `svg/SVGPathSegLinetoAbs.idl` **[S12]** - SVGPathSegLinetoAbs: x, y.
- `svg/SVGPathSegLinetoHorizontalAbs.idl` **[S12]** - SVGPathSegLinetoHorizontalAbs: x.
- `svg/SVGPathSegLinetoHorizontalRel.idl` **[S12]** - SVGPathSegLinetoHorizontalRel: x.
- `svg/SVGPathSegLinetoRel.idl` **[S12]** - SVGPathSegLinetoRel: x, y.
- `svg/SVGPathSegLinetoVerticalAbs.idl` **[S12]** - SVGPathSegLinetoVerticalAbs: y.
- `svg/SVGPathSegLinetoVerticalRel.idl` **[S12]** - SVGPathSegLinetoVerticalRel: y.
- `svg/SVGPathSegList.idl` **[S12]** - SVGPathSegList: length, numberOfItems, clear, initialize, getItem, insertItemBefore, replaceItem, removeItem, appendItem, <indexed operation>.
- `svg/SVGPathSegMovetoAbs.idl` **[S12]** - SVGPathSegMovetoAbs: x, y.
- `svg/SVGPathSegMovetoRel.idl` **[S12]** - SVGPathSegMovetoRel: x, y.
- `svg/SVGPatternElement.idl` **[S08]** - SVGPatternElement: patternUnits, patternContentUnits, patternTransform, x, y, width, height; includes: SVGPatternElement -> SVGFitToViewBox, SVGPatternElement -> SVGTests, SVGPatternElement -> SVGURIReference.
- `svg/SVGPoint.idl` **[S04]** - SVGPoint: x, y, matrixTransform.
- `svg/SVGPointList.idl` **[S04]** - SVGPointList: length, numberOfItems, clear, initialize, getItem, insertItemBefore, replaceItem, removeItem, appendItem, <indexed operation>.
- `svg/SVGPolygonElement.idl` **[S03]** - SVGPolygonElement: points, animatedPoints.
- `svg/SVGPolylineElement.idl` **[S03]** - SVGPolylineElement: points, animatedPoints.
- `svg/SVGPreserveAspectRatio.idl` **[S04]** - SVGPreserveAspectRatio: align, meetOrSlice.
- `svg/SVGRadialGradientElement.idl` **[S08]** - SVGRadialGradientElement: cx, cy, r, fx, fy, fr.
- `svg/SVGRect.idl` **[S04]** - SVGRect: x, y, width, height.
- `svg/SVGRectElement.idl` **[S03]** - SVGRectElement: x, y, width, height, rx, ry.
- `svg/SVGScriptElement.idl` **[D09, S11]** - SVGScriptElement: type, async; includes: SVGScriptElement -> SVGURIReference.
- `svg/SVGSetElement.idl` **[S04, S07]** - SVGSetElement: no own parsed members.
- `svg/SVGStopElement.idl` **[S08]** - SVGStopElement: offset.
- `svg/SVGStringList.idl` **[S04]** - SVGStringList: length, numberOfItems, clear, initialize, getItem, insertItemBefore, replaceItem, removeItem, appendItem, <indexed operation>.
- `svg/SVGStyleElement.idl` **[D09, S11]** - SVGStyleElement: disabled, type, media, title; includes: SVGStyleElement -> LinkStyle.
- `svg/SVGSVGElement.idl` **[S06]** - SVGSVGElement: x, y, width, height, currentScale, currentTranslate, useCurrentView, currentView, getIntersectionList, getEnclosureList, checkIntersection, checkEnclosure, deselectAll, createSVGNumber, createSVGLength, createSVGAngle, createSVGPoint, createSVGMatrix, createSVGRect, createSVGTransform, createSVGTransformFromMatrix, getElementById, pauseAnimations, unpauseAnimations, animationsPaused, getCurrentTime, setCurrentTime, suspendRedraw, unsuspendRedraw, unsuspendRedrawAll, forceRedraw; includes: SVGSVGElement -> SVGFitToViewBox, SVGSVGElement -> SVGZoomAndPan.
- `svg/SVGSwitchElement.idl` **[D09, S11]** - SVGSwitchElement: no own parsed members.
- `svg/SVGSymbolElement.idl` **[D09, S11]** - SVGSymbolElement: no own parsed members; includes: SVGSymbolElement -> SVGFitToViewBox.
- `svg/SVGTests.idl` **[D09, S11]** - SVGTests (mixin): requiredExtensions, systemLanguage.
- `svg/SVGTextContentElement.idl` **[S05]** - SVGTextContentElement: textLength, lengthAdjust, getNumberOfChars, getComputedTextLength, getSubStringLength, getStartPositionOfChar, getEndPositionOfChar, getExtentOfChar, getRotationOfChar, getCharNumAtPosition, selectSubString.
- `svg/SVGTextElement.idl` **[S05]** - SVGTextElement: no own parsed members.
- `svg/SVGTextPathElement.idl` **[S05]** - SVGTextPathElement: startOffset, method, spacing; includes: SVGTextPathElement -> SVGURIReference.
- `svg/SVGTextPositioningElement.idl` **[S05]** - SVGTextPositioningElement: x, y, dx, dy, rotate.
- `svg/SVGTitleElement.idl` **[D09, S11]** - SVGTitleElement: no own parsed members.
- `svg/SVGTransform.idl` **[S04]** - SVGTransform: type, matrix, angle, setMatrix, setTranslate, setScale, setRotate, setSkewX, setSkewY.
- `svg/SVGTransformList.idl` **[S04]** - SVGTransformList: length, numberOfItems, clear, initialize, getItem, insertItemBefore, replaceItem, removeItem, appendItem, <indexed operation>, createSVGTransformFromMatrix, consolidate.
- `svg/SVGTRefElement.idl` **[S12]** - SVGTRefElement: no own parsed members; includes: SVGTRefElement -> SVGURIReference.
- `svg/SVGTSpanElement.idl` **[S05]** - SVGTSpanElement: no own parsed members.
- `svg/SVGUnitTypes.idl` **[D09, S11]** - SVGUnitTypes: no own parsed members.
- `svg/SVGURIReference.idl` **[D09, S11]** - SVGURIReference (mixin): href.
- `svg/SVGUseElement.idl` **[D09, S11]** - SVGUseElement: x, y, width, height; includes: SVGUseElement -> SVGURIReference.
- `svg/SVGViewElement.idl` **[S06]** - SVGViewElement: no own parsed members; includes: SVGViewElement -> SVGFitToViewBox, SVGViewElement -> SVGZoomAndPan.
- `svg/SVGViewSpec.idl` **[S06]** - SVGViewSpec: transform, viewBoxString, preserveAspectRatioString, transformString; includes: SVGViewSpec -> SVGFitToViewBox, SVGViewSpec -> SVGZoomAndPan.
- `svg/SVGZoomAndPan.idl` **[S06]** - SVGZoomAndPan (mixin): zoomAndPan.
### workers

- `workers/AbstractWorker.idl` **[B17, B25]** - AbstractWorker (mixin): onerror.
- `workers/DedicatedWorkerGlobalScope.idl` **[B17, B25]** - DedicatedWorkerGlobalScope: name, postMessage, close, onmessage, onmessageerror; includes: DedicatedWorkerGlobalScope -> AnimationFrameProvider, DedicatedWorkerGlobalScope -> RTCRtpScriptTransformProvider.
- `workers/service/background-fetch/BackgroundFetchEvent.idl` **[B17, B25]** - BackgroundFetchEvent: constructor, registration.
- `workers/service/background-fetch/BackgroundFetchEventInit.idl` **[B17, B25]** - dictionaries: BackgroundFetchEventInit.
- `workers/service/background-fetch/BackgroundFetchFailureReason.idl` **[B17, B25]** - enums: BackgroundFetchFailureReason.
- `workers/service/background-fetch/BackgroundFetchManager.idl` **[B17, B25]** - BackgroundFetchManager: fetch, get, getIds.
- `workers/service/background-fetch/BackgroundFetchOptions.idl` **[B17, B25]** - dictionaries: BackgroundFetchOptions.
- `workers/service/background-fetch/BackgroundFetchRecord.idl` **[B17, B25]** - BackgroundFetchRecord: request, responseReady.
- `workers/service/background-fetch/BackgroundFetchRegistration.idl` **[B17, B25]** - BackgroundFetchRegistration: id, uploadTotal, uploaded, downloadTotal, downloaded, result, failureReason, recordsAvailable, onprogress, abort, match, matchAll.
- `workers/service/background-fetch/BackgroundFetchResult.idl` **[B17, B25]** - enums: BackgroundFetchResult.
- `workers/service/background-fetch/BackgroundFetchUIOptions.idl` **[B17, B25]** - dictionaries: BackgroundFetchUIOptions.
- `workers/service/background-fetch/BackgroundFetchUpdateUIEvent.idl` **[B17, B25]** - BackgroundFetchUpdateUIEvent: constructor, updateUI.
- `workers/service/background-fetch/ImageResource.idl` **[B17, B25]** - dictionaries: ImageResource.
- `workers/service/background-fetch/ServiceWorkerGlobalScope+BackgroundFetchAPI.idl` **[B17, B25]** - ServiceWorkerGlobalScope (partial): onbackgroundfetchsuccess, onbackgroundfetchfail, onbackgroundfetchabort, onbackgroundfetchclick.
- `workers/service/background-fetch/ServiceWorkerRegistration+BackgroundFetchAPI.idl` **[B17, B25]** - ServiceWorkerRegistration (partial): backgroundFetch.
- `workers/service/ExtendableEvent.idl` **[B17, B25]** - ExtendableEvent: constructor, waitUntil.
- `workers/service/ExtendableEventInit.idl` **[B17, B25]** - dictionaries: ExtendableEventInit.
- `workers/service/ExtendableMessageEvent.idl` **[B17, B25]** - ExtendableMessageEvent: constructor, data, origin, lastEventId, source, ports; dictionaries: ExtendableMessageEventInit.
- `workers/service/FetchEvent.idl` **[B17, B25]** - FetchEvent: constructor, request, preloadResponse, clientId, resultingClientId, handled, respondWith; dictionaries: FetchEventInit.
- `workers/service/InstallEvent.idl` **[B17, B25]** - InstallEvent: constructor, addRoutes.
- `workers/service/NavigationPreloadManager.idl` **[B17, B25]** - NavigationPreloadManager: enable, disable, setHeaderValue, getState.
- `workers/service/NavigationPreloadState.idl` **[B17, B25]** - dictionaries: NavigationPreloadState.
- `workers/service/RouterCondition.idl` **[B17, B25]** - dictionaries: RouterCondition.
- `workers/service/RouterRule.idl` **[B17, B25]** - dictionaries: RouterRule.
- `workers/service/RouterSourceDict.idl` **[B17, B25]** - dictionaries: RouterSourceDict.
- `workers/service/RouterSourceEnum.idl` **[B17, B25]** - enums: RouterSourceEnum.
- `workers/service/RunningStatus.idl` **[B17, B25]** - enums: RunningStatus.
- `workers/service/ServiceWorker.idl` **[B17, B25]** - ServiceWorker: scriptURL, state, postMessage, onstatechange; enums: ServiceWorkerState; includes: ServiceWorker -> AbstractWorker.
- `workers/service/ServiceWorkerClient.idl` **[B17, B25]** - ServiceWorkerClient: url, frameType, type, id, postMessage; enums: FrameType.
- `workers/service/ServiceWorkerClients.idl` **[B17, B25]** - ServiceWorkerClients: get, matchAll, openWindow, claim; dictionaries: ClientQueryOptions.
- `workers/service/ServiceWorkerClientType.idl` **[B17, B25]** - enums: ServiceWorkerClientType.
- `workers/service/ServiceWorkerContainer.idl` **[B17, B25]** - ServiceWorkerContainer: controller, ready, register, getRegistration, getRegistrations, startMessages, oncontrollerchange, onmessage, onmessageerror; dictionaries: RegistrationOptions.
- `workers/service/ServiceWorkerGlobalScope.idl` **[B17, B25]** - ServiceWorkerGlobalScope: clients, registration, serviceWorker, skipWaiting, cookieStore, oncookiechange, oninstall, onactivate, onfetch, onmessage, onmessageerror, onnotificationclick, onnotificationclose.
- `workers/service/ServiceWorkerRegistration.idl` **[B17, B25]** - ServiceWorkerRegistration: installing, waiting, active, navigationPreload, scope, updateViaCache, cookies, update, unregister, showNotification, getNotifications, onupdatefound; dictionaries: GetNotificationOptions.
- `workers/service/ServiceWorkerUpdateViaCache.idl` **[B17, B25]** - enums: ServiceWorkerUpdateViaCache.
- `workers/service/ServiceWorkerWindowClient.idl` **[B17, B25]** - ServiceWorkerWindowClient: visibilityState, focused, ancestorOrigins, focus, navigate.
- `workers/shared/SharedWorker.idl` **[B17, B25]** - SharedWorker: constructor, port; includes: SharedWorker -> AbstractWorker.
- `workers/shared/SharedWorkerGlobalScope.idl` **[B17, B25]** - SharedWorkerGlobalScope: name, close, onconnect.
- `workers/Worker.idl` **[B17, B25]** - Worker: constructor, terminate, postMessage, onmessage, onmessageerror; includes: Worker -> AbstractWorker.
- `workers/WorkerGlobalScope.idl` **[B17, B25]** - WorkerGlobalScope: self, location, navigator, importScripts, onerror, onoffline, ononline, onrejectionhandled, onunhandledrejection; includes: WorkerGlobalScope -> WindowOrWorkerGlobalScope, WorkerGlobalScope -> FontFaceSource.
- `workers/WorkerLocation.idl` **[B17, B25]** - WorkerLocation: href, protocol, host, hostname, port, pathname, search, hash, origin.
- `workers/WorkerOptions.idl` **[B17, B25]** - dictionaries: WorkerOptions.
- `workers/WorkerType.idl` **[B17, B25]** - enums: WorkerType.
### worklets

- `worklets/PaintWorkletGlobalScope.idl` **[B16]** - PaintWorkletGlobalScope: registerPaint, devicePixelRatio.
- `worklets/Worklet.idl` **[B16]** - Worklet: addModule.
- `worklets/WorkletGlobalScope.idl` **[B16]** - WorkletGlobalScope: no own parsed members.
- `worklets/WorkletOptions.idl` **[B16]** - dictionaries: WorkletOptions.
### xml

- `xml/CustomXPathNSResolver.idl` **[D05]** - callbacks: CustomXPathNSResolver.
- `xml/DOMParser.idl` **[D04]** - DOMParser: constructor, parseFromString.
- `xml/XMLHttpRequest.idl` **[B18]** - XMLHttpRequest: constructor, onreadystatechange, readyState, open, setRequestHeader, timeout, withCredentials, upload, send, abort, responseURL, status, statusText, getResponseHeader, getAllResponseHeaders, overrideMimeType, responseType, response, responseText, responseXML; enums: XMLHttpRequestResponseType.
- `xml/XMLHttpRequestEventTarget.idl` **[B18]** - XMLHttpRequestEventTarget: onloadstart, onprogress, onabort, onerror, onload, ontimeout, onloadend.
- `xml/XMLHttpRequestProgressEvent.idl` **[B18]** - XMLHttpRequestProgressEvent: position, totalSize.
- `xml/XMLHttpRequestUpload.idl` **[B18]** - XMLHttpRequestUpload: no own parsed members.
- `xml/XMLSerializer.idl` **[D04]** - XMLSerializer: constructor, serializeToString.
- `xml/XPathEvaluator.idl` **[D05]** - XPathEvaluator: constructor; includes: XPathEvaluator -> XPathEvaluatorBase.
- `xml/XPathEvaluatorBase.idl` **[D05]** - XPathEvaluatorBase (mixin): createExpression, createNSResolver, evaluate.
- `xml/XPathExpression.idl` **[D05]** - XPathExpression: evaluate.
- `xml/XPathNSResolver.idl` **[D05]** - XPathNSResolver: lookupNamespaceURI.
- `xml/XPathResult.idl` **[D05]** - XPathResult: resultType, numberValue, stringValue, booleanValue, singleNodeValue, invalidIteratorState, snapshotLength, iterateNext, snapshotItem.
- `xml/XSLTProcessor.idl` **[D06]** - XSLTProcessor: constructor, importStylesheet, transformToFragment, transformToDocument, setParameter, getParameter, removeParameter, clearParameters, reset.

## Preprocessor alternatives

The parser models the undefined-macro/#else path of IDL preprocessor conditionals. The complete comment-stripped source of files containing such alternatives is retained here so other declarations are available for review; no branch is silently treated as universally supported.

### dom/DeviceOrientationEvent.idl

```webidl
[
    Conditional=DEVICE_ORIENTATION,
    Exposed=Window,
    SecureContext
] interface DeviceOrientationEvent : Event {
    readonly attribute unrestricted double? alpha;
    readonly attribute unrestricted double? beta;
    readonly attribute unrestricted double? gamma;

    [CallWith=CurrentDocument, EnabledBySetting=DeviceOrientationPermissionAPIEnabled] static Promise<DeviceOrientationOrMotionPermissionState> requestPermission();

    
    
#if defined(WTF_PLATFORM_IOS_FAMILY) && WTF_PLATFORM_IOS_FAMILY
    [ImplementedAs=compassHeading] readonly attribute unrestricted double? webkitCompassHeading;
    [ImplementedAs=compassAccuracy] readonly attribute unrestricted double? webkitCompassAccuracy;
    undefined initDeviceOrientationEvent(optional [AtomString] DOMString type = "",
                                    optional boolean bubbles = false,
                                    optional boolean cancelable = false,
                                    optional unrestricted double? alpha = null,
                                    optional unrestricted double? beta = null,
                                    optional unrestricted double? gamma = null,
                                    optional unrestricted double? compassHeading = null,
                                    optional unrestricted double? compassAccuracy = null);
#else
    readonly attribute boolean? absolute;
    undefined initDeviceOrientationEvent(optional [AtomString] DOMString type = "",
                                    optional boolean bubbles = false,
                                    optional boolean cancelable = false,
                                    optional unrestricted double? alpha = null,
                                    optional unrestricted double? beta = null,
                                    optional unrestricted double? gamma = null,
                                    optional boolean? absolute = null);
#endif
};
```

### dom/Document.idl

```webidl
typedef (
#if defined(ENABLE_WEBGL) && ENABLE_WEBGL
    WebGLRenderingContext or
    WebGL2RenderingContext or
#endif
    GPUCanvasContext or
    ImageBitmapRenderingContext or 
    CanvasRenderingContext2D) RenderingContext;

typedef (HTMLScriptElement or SVGScriptElement) HTMLOrSVGScriptElement;

[
    CustomHeapSnapshot,
    DOMJIT,
    ExportMacro=WEBCORE_EXPORT,
    JSCustomHeader,
    JSCustomMarkFunction,
    JSGenerateToNativeObject,
    Exposed=Window
] interface Document : Node {
    [CallWith=CurrentDocument] constructor();

    [CallWith=CurrentDocument] static Document parseHTMLUnsafe((TrustedHTML or DOMString) html);

    [SameObject, ImplementedAs=fragmentDirectiveForBindings, EnabledBySetting=ScrollToTextFragmentFeatureDetectionEnabled] readonly attribute FragmentDirective fragmentDirective;
    [SameObject] readonly attribute DOMImplementation implementation;
    [ImplementedAs=urlForBindings] readonly attribute USVString URL;
    [ImplementedAs=urlForBindings] readonly attribute USVString documentURI;
    readonly attribute DOMString compatMode;
    [ImplementedAs=characterSetWithUTF8Fallback] readonly attribute DOMString characterSet;
    [ImplementedAs=characterSetWithUTF8Fallback] readonly attribute DOMString charset; 
    [ImplementedAs=characterSetWithUTF8Fallback] readonly attribute DOMString inputEncoding; 
    readonly attribute DOMString contentType;

    readonly attribute DocumentType? doctype;
    [DOMJIT=Getter] readonly attribute Element? documentElement;

    [NewObject, ImplementedAs=createElementForBindings] Element createElement([AtomString] DOMString localName, optional (DOMString or ElementCreationOptions) options = {});
    [NewObject] Element createElementNS([AtomString] DOMString? namespaceURI, [AtomString] DOMString qualifiedName, optional (DOMString or ElementCreationOptions) options = {});
    HTMLCollection getElementsByTagName([AtomString] DOMString qualifiedName);
    HTMLCollection getElementsByTagNameNS([AtomString] DOMString? namespaceURI, [AtomString] DOMString localName);
    HTMLCollection getElementsByClassName([AtomString] DOMString classNames);

    [EnabledBySetting=ScopedCustomElementRegistryEnabled, ImplementedAs=customElementRegistryForBindings] readonly attribute CustomElementRegistry? customElementRegistry;

    [NewObject] DocumentFragment createDocumentFragment();
    [NewObject] Text createTextNode(DOMString data);
    [NewObject] CDATASection createCDATASection(DOMString data);
    [NewObject] Comment createComment(DOMString data);
    [NewObject] ProcessingInstruction createProcessingInstruction(DOMString target, DOMString data);

    [CEReactions=Needed, NewObject] Node importNode(Node node, optional (boolean or ImportNodeOptions) options = false);
    [CEReactions=Needed] Node adoptNode(Node node);

    [NewObject] Attr createAttribute([AtomString] DOMString localName);
    [NewObject] Attr createAttributeNS([AtomString] DOMString? namespaceURI, [AtomString] DOMString qualifiedName);

    [NewObject] Event createEvent(DOMString type);

    [NewObject] Range createRange();

    
    [NewObject] NodeIterator createNodeIterator(Node root, optional unsigned long whatToShow = 0xFFFFFFFF, optional NodeFilter? filter = null);
    [NewObject] TreeWalker createTreeWalker(Node root, optional unsigned long whatToShow = 0xFFFFFFFF, optional NodeFilter? filter = null);

    
    readonly attribute DOMString? xmlEncoding;
    attribute DOMString? xmlVersion;
    attribute boolean xmlStandalone;

    
    Range? caretRangeFromPoint(optional long x = 0, optional long y = 0);

    
    RenderingContext? getCSSCanvasContext(DOMString contextId, DOMString name, long width, long height);

    [EnabledBySetting=IsARIANotifyEnabled] undefined ariaNotify(DOMString announcement);
    [EnabledBySetting=IsARIANotifyEnabled] undefined ariaNotify(DOMString announcement, AriaNotifyOptions options);
};

enum DocumentReadyState { "loading", "interactive", "complete" };

Document includes ParentNode;
Document includes NonElementParentNode;
Document includes DocumentOrShadowRoot;
Document includes GlobalEventHandlers;
Document includes DocumentAndElementEventHandlers;
Document includes FontFaceSource;
Document includes XPathEvaluatorBase;
```

### html/canvas/CanvasDrawImage.idl

```webidl
typedef (HTMLImageElement
    or SVGImageElement
    or HTMLCanvasElement
    or ImageBitmap
    or CSSStyleImageValue
#if defined(ENABLE_OFFSCREEN_CANVAS) && ENABLE_OFFSCREEN_CANVAS
    or OffscreenCanvas
#endif
#if defined(ENABLE_VIDEO) && ENABLE_VIDEO
    or HTMLVideoElement
#endif
#if defined(ENABLE_WEB_CODECS) && ENABLE_WEB_CODECS
    or WebCodecsVideoFrame
#endif
) CanvasImageSource;


interface mixin CanvasDrawImage {
    
    undefined drawImage(CanvasImageSource image, unrestricted double dx, unrestricted double dy);
    undefined drawImage(CanvasImageSource image, unrestricted double dx, unrestricted double dy, unrestricted double dw, unrestricted double dh);
    undefined drawImage(CanvasImageSource image, unrestricted double sx, unrestricted double sy, unrestricted double sw, unrestricted double sh, unrestricted double dx, unrestricted double dy, unrestricted double dw, unrestricted double dh);
};
```

### html/canvas/CanvasFillStrokeStyles.idl

```webidl
typedef (HTMLImageElement
    or SVGImageElement
    or HTMLCanvasElement
    or ImageBitmap
    or CSSStyleImageValue
#if defined(ENABLE_OFFSCREEN_CANVAS) && ENABLE_OFFSCREEN_CANVAS
    or OffscreenCanvas
#endif
#if defined(ENABLE_VIDEO) && ENABLE_VIDEO
    or HTMLVideoElement
#endif
#if defined(ENABLE_WEB_CODECS) && ENABLE_WEB_CODECS
    or WebCodecsVideoFrame
#endif
) CanvasImageSource;


interface mixin CanvasFillStrokeStyles {
    
    [OptimizedUnionSetterUsingOverloads] attribute (DOMString or CanvasGradient or CanvasPattern) strokeStyle; 
    [OptimizedUnionSetterUsingOverloads] attribute (DOMString or CanvasGradient or CanvasPattern) fillStyle; 

    CanvasGradient createLinearGradient(double x0, double y0, double x1, double y1);
    CanvasGradient createRadialGradient(double x0, double y0, double r0, double x1, double y1, double r1);
    CanvasGradient createConicGradient(double angle, double x, double y);
    CanvasPattern? createPattern(CanvasImageSource image, [LegacyNullToEmptyString] DOMString repetition);
};
```

### html/canvas/GPUCanvasContext.idl

```webidl
[
    ActiveDOMObject,
    EnabledBySetting=WebGPUEnabled,
    ReportExtraMemoryCost,
    ReportExternalMemoryCost,
    Exposed=(Window,Worker),
    SecureContext,
    SkipVTableValidation,
]
interface GPUCanvasContext {
    readonly attribute (HTMLCanvasElement
#if defined(ENABLE_OFFSCREEN_CANVAS) && ENABLE_OFFSCREEN_CANVAS
        or OffscreenCanvas
#endif
    ) canvas;

    undefined configure(GPUCanvasConfiguration configuration);
    undefined unconfigure();

    GPUCanvasConfiguration? getConfiguration();
    GPUTexture getCurrentTexture();
};
```

### html/canvas/ImageBitmapRenderingContext.idl

```webidl
#if defined(ENABLE_OFFSCREEN_CANVAS)
typedef (HTMLCanvasElement or OffscreenCanvas) ImageBitmapCanvas;
#else
typedef (HTMLCanvasElement) ImageBitmapCanvas;
#endif

[
    ReportExtraMemoryCost,
    ReportExternalMemoryCost,
    Exposed=(Window,Worker),
    CallTracer=InspectorCanvasCallTracer,
] interface ImageBitmapRenderingContext {
    readonly attribute ImageBitmapCanvas canvas;

    undefined transferFromImageBitmap(ImageBitmap? bitmap);
};
```

### html/canvas/PredefinedColorSpace.idl

```webidl
enum PredefinedColorSpace {
      "srgb"
    , "srgb-linear"
#if defined(ENABLE_PREDEFINED_COLOR_SPACE_DISPLAY_P3) && ENABLE_PREDEFINED_COLOR_SPACE_DISPLAY_P3
    , "display-p3"
    , "display-p3-linear"
#endif
};
```

### html/canvas/WebGL2RenderingContext.idl

```webidl
typedef unsigned long      GLenum;
typedef boolean            GLboolean;
typedef unsigned long      GLbitfield;
typedef byte               GLbyte;
typedef short              GLshort;
typedef long               GLint;
typedef long               GLsizei;
typedef long long          GLintptr;
typedef long long          GLsizeiptr;
typedef long long          GLint64;
typedef octet              GLubyte;
typedef unsigned short     GLushort;
typedef unsigned long      GLuint;
typedef unsigned long long GLuint64;
typedef unrestricted float GLfloat;
typedef unrestricted float GLclampf;
typedef (ArrayBuffer or ArrayBufferView) BufferDataSource;
typedef ([AllowShared] Float32Array or sequence<GLfloat>) Float32List;
typedef ([AllowShared] Int32Array or sequence<GLint>) Int32List;
typedef ([AllowShared] Uint32Array or sequence<GLuint>) Uint32List;

typedef (ImageBitmap or ImageData or HTMLImageElement or HTMLCanvasElement
#if defined(ENABLE_VIDEO) && ENABLE_VIDEO
    or HTMLVideoElement
#endif
#if defined(ENABLE_OFFSCREEN_CANVAS) && ENABLE_OFFSCREEN_CANVAS
    or OffscreenCanvas
#endif
#if defined(ENABLE_WEB_CODECS) && ENABLE_WEB_CODECS
    or WebCodecsVideoFrame
#endif
) TexImageSource;

[
    ActiveDOMObject,
    Conditional=WEBGL,
    EnabledBySetting=WebGLEnabled,
    GenerateIsReachable=ImplCanvasBase,
    JSCustomMarkFunction,
    DoNotCheckConstants,
    ExportMacro=WEBCORE_EXPORT,
    ReportExtraMemoryCost,
    ReportExternalMemoryCost,
    Exposed=(Window,Worker),
    CallTracer=InspectorCanvasCallTracer,
] interface WebGL2RenderingContext {

    
    

    

    const GLenum READ_BUFFER                                   = 0x0C02;
    const GLenum UNPACK_ROW_LENGTH                             = 0x0CF2;
    const GLenum UNPACK_SKIP_ROWS                              = 0x0CF3;
    const GLenum UNPACK_SKIP_PIXELS                            = 0x0CF4;
    const GLenum PACK_ROW_LENGTH                               = 0x0D02;
    const GLenum PACK_SKIP_ROWS                                = 0x0D03;
    const GLenum PACK_SKIP_PIXELS                              = 0x0D04;
    const GLenum COLOR                                         = 0x1800;
    const GLenum DEPTH                                         = 0x1801;
    const GLenum STENCIL                                       = 0x1802;
    const GLenum RED                                           = 0x1903;
    const GLenum RGB8                                          = 0x8051;
    const GLenum RGBA8                                         = 0x8058;
    const GLenum RGB10_A2                                      = 0x8059;
    const GLenum TEXTURE_BINDING_3D                            = 0x806A;
    const GLenum UNPACK_SKIP_IMAGES                            = 0x806D;
    const GLenum UNPACK_IMAGE_HEIGHT                           = 0x806E;
    const GLenum TEXTURE_3D                                    = 0x806F;
    const GLenum TEXTURE_WRAP_R                                = 0x8072;
    const GLenum MAX_3D_TEXTURE_SIZE                           = 0x8073;
    const GLenum UNSIGNED_INT_2_10_10_10_REV                   = 0x8368;
    const GLenum MAX_ELEMENTS_VERTICES                         = 0x80E8;
    const GLenum MAX_ELEMENTS_INDICES                          = 0x80E9;
    const GLenum TEXTURE_MIN_LOD                               = 0x813A;
    const GLenum TEXTURE_MAX_LOD                               = 0x813B;
    const GLenum TEXTURE_BASE_LEVEL                            = 0x813C;
    const GLenum TEXTURE_MAX_LEVEL                             = 0x813D;
    const GLenum MIN                                           = 0x8007;
    const GLenum MAX                                           = 0x8008;
    const GLenum DEPTH_COMPONENT24                             = 0x81A6;
    const GLenum MAX_TEXTURE_LOD_BIAS                          = 0x84FD;
    const GLenum TEXTURE_COMPARE_MODE                          = 0x884C;
    const GLenum TEXTURE_COMPARE_FUNC                          = 0x884D;
    const GLenum CURRENT_QUERY                                 = 0x8865;
    const GLenum QUERY_RESULT                                  = 0x8866;
    const GLenum QUERY_RESULT_AVAILABLE                        = 0x8867;
    const GLenum STREAM_READ                                   = 0x88E1;
    const GLenum STREAM_COPY                                   = 0x88E2;
    const GLenum STATIC_READ                                   = 0x88E5;
    const GLenum STATIC_COPY                                   = 0x88E6;
    const GLenum DYNAMIC_READ                                  = 0x88E9;
    const GLenum DYNAMIC_COPY                                  = 0x88EA;
    const GLenum MAX_DRAW_BUFFERS                              = 0x8824;
    const GLenum DRAW_BUFFER0                                  = 0x8825;
    const GLenum DRAW_BUFFER1                                  = 0x8826;
    const GLenum DRAW_BUFFER2                                  = 0x8827;
    const GLenum DRAW_BUFFER3                                  = 0x8828;
    const GLenum DRAW_BUFFER4                                  = 0x8829;
    const GLenum DRAW_BUFFER5                                  = 0x882A;
    const GLenum DRAW_BUFFER6                                  = 0x882B;
    const GLenum DRAW_BUFFER7                                  = 0x882C;
    const GLenum DRAW_BUFFER8                                  = 0x882D;
    const GLenum DRAW_BUFFER9                                  = 0x882E;
    const GLenum DRAW_BUFFER10                                 = 0x882F;
    const GLenum DRAW_BUFFER11                                 = 0x8830;
    const GLenum DRAW_BUFFER12                                 = 0x8831;
    const GLenum DRAW_BUFFER13                                 = 0x8832;
    const GLenum DRAW_BUFFER14                                 = 0x8833;
    const GLenum DRAW_BUFFER15                                 = 0x8834;
    const GLenum MAX_FRAGMENT_UNIFORM_COMPONENTS               = 0x8B49;
    const GLenum MAX_VERTEX_UNIFORM_COMPONENTS                 = 0x8B4A;
    const GLenum SAMPLER_3D                                    = 0x8B5F;
    const GLenum SAMPLER_2D_SHADOW                             = 0x8B62;
    const GLenum FRAGMENT_SHADER_DERIVATIVE_HINT               = 0x8B8B;
    const GLenum PIXEL_PACK_BUFFER                             = 0x88EB;
    const GLenum PIXEL_UNPACK_BUFFER                           = 0x88EC;
    const GLenum PIXEL_PACK_BUFFER_BINDING                     = 0x88ED;
    const GLenum PIXEL_UNPACK_BUFFER_BINDING                   = 0x88EF;
    const GLenum FLOAT_MAT2x3                                  = 0x8B65;
    const GLenum FLOAT_MAT2x4                                  = 0x8B66;
    const GLenum FLOAT_MAT3x2                                  = 0x8B67;
    const GLenum FLOAT_MAT3x4                                  = 0x8B68;
    const GLenum FLOAT_MAT4x2                                  = 0x8B69;
    const GLenum FLOAT_MAT4x3                                  = 0x8B6A;
    const GLenum SRGB                                          = 0x8C40;
    const GLenum SRGB8                                         = 0x8C41;
    const GLenum SRGB8_ALPHA8                                  = 0x8C43;
    const GLenum COMPARE_REF_TO_TEXTURE                        = 0x884E;
    const GLenum RGBA32F                                       = 0x8814;
    const GLenum RGB32F                                        = 0x8815;
    const GLenum RGBA16F                                       = 0x881A;
    const GLenum RGB16F                                        = 0x881B;
    const GLenum VERTEX_ATTRIB_ARRAY_INTEGER                   = 0x88FD;
    const GLenum MAX_ARRAY_TEXTURE_LAYERS                      = 0x88FF;
    const GLenum MIN_PROGRAM_TEXEL_OFFSET                      = 0x8904;
    const GLenum MAX_PROGRAM_TEXEL_OFFSET                      = 0x8905;
    const GLenum MAX_VARYING_COMPONENTS                        = 0x8B4B;
    const GLenum TEXTURE_2D_ARRAY                              = 0x8C1A;
    const GLenum TEXTURE_BINDING_2D_ARRAY                      = 0x8C1D;
    const GLenum R11F_G11F_B10F                                = 0x8C3A;
    const GLenum UNSIGNED_INT_10F_11F_11F_REV                  = 0x8C3B;
    const GLenum RGB9_E5                                       = 0x8C3D;
    const GLenum UNSIGNED_INT_5_9_9_9_REV                      = 0x8C3E;
    const GLenum TRANSFORM_FEEDBACK_BUFFER_MODE                = 0x8C7F;
    const GLenum MAX_TRANSFORM_FEEDBACK_SEPARATE_COMPONENTS    = 0x8C80;
    const GLenum TRANSFORM_FEEDBACK_VARYINGS                   = 0x8C83;
    const GLenum TRANSFORM_FEEDBACK_BUFFER_START               = 0x8C84;
    const GLenum TRANSFORM_FEEDBACK_BUFFER_SIZE                = 0x8C85;
    const GLenum TRANSFORM_FEEDBACK_PRIMITIVES_WRITTEN         = 0x8C88;
    const GLenum RASTERIZER_DISCARD                            = 0x8C89;
    const GLenum MAX_TRANSFORM_FEEDBACK_INTERLEAVED_COMPONENTS = 0x8C8A;
    const GLenum MAX_TRANSFORM_FEEDBACK_SEPARATE_ATTRIBS       = 0x8C8B;
    const GLenum INTERLEAVED_ATTRIBS                           = 0x8C8C;
    const GLenum SEPARATE_ATTRIBS                              = 0x8C8D;
    const GLenum TRANSFORM_FEEDBACK_BUFFER                     = 0x8C8E;
    const GLenum TRANSFORM_FEEDBACK_BUFFER_BINDING             = 0x8C8F;
    const GLenum RGBA32UI                                      = 0x8D70;
    const GLenum RGB32UI                                       = 0x8D71;
    const GLenum RGBA16UI                                      = 0x8D76;
    const GLenum RGB16UI                                       = 0x8D77;
    const GLenum RGBA8UI                                       = 0x8D7C;
    const GLenum RGB8UI                                        = 0x8D7D;
    const GLenum RGBA32I                                       = 0x8D82;
    const GLenum RGB32I                                        = 0x8D83;
    const GLenum RGBA16I                                       = 0x8D88;
    const GLenum RGB16I                                        = 0x8D89;
    const GLenum RGBA8I                                        = 0x8D8E;
    const GLenum RGB8I                                         = 0x8D8F;
    const GLenum RED_INTEGER                                   = 0x8D94;
    const GLenum RGB_INTEGER                                   = 0x8D98;
    const GLenum RGBA_INTEGER                                  = 0x8D99;
    const GLenum SAMPLER_2D_ARRAY                              = 0x8DC1;
    const GLenum SAMPLER_2D_ARRAY_SHADOW                       = 0x8DC4;
    const GLenum SAMPLER_CUBE_SHADOW                           = 0x8DC5;
    const GLenum UNSIGNED_INT_VEC2                             = 0x8DC6;
    const GLenum UNSIGNED_INT_VEC3                             = 0x8DC7;
    const GLenum UNSIGNED_INT_VEC4                             = 0x8DC8;
    const GLenum INT_SAMPLER_2D                                = 0x8DCA;
    const GLenum INT_SAMPLER_3D                                = 0x8DCB;
    const GLenum INT_SAMPLER_CUBE                              = 0x8DCC;
    const GLenum INT_SAMPLER_2D_ARRAY                          = 0x8DCF;
    const GLenum UNSIGNED_INT_SAMPLER_2D                       = 0x8DD2;
    const GLenum UNSIGNED_INT_SAMPLER_3D                       = 0x8DD3;
    const GLenum UNSIGNED_INT_SAMPLER_CUBE                     = 0x8DD4;
    const GLenum UNSIGNED_INT_SAMPLER_2D_ARRAY                 = 0x8DD7;
    const GLenum DEPTH_COMPONENT32F                            = 0x8CAC;
    const GLenum DEPTH32F_STENCIL8                             = 0x8CAD;
    const GLenum FLOAT_32_UNSIGNED_INT_24_8_REV                = 0x8DAD;
    const GLenum FRAMEBUFFER_ATTACHMENT_COLOR_ENCODING         = 0x8210;
    const GLenum FRAMEBUFFER_ATTACHMENT_COMPONENT_TYPE         = 0x8211;
    const GLenum FRAMEBUFFER_ATTACHMENT_RED_SIZE               = 0x8212;
    const GLenum FRAMEBUFFER_ATTACHMENT_GREEN_SIZE             = 0x8213;
    const GLenum FRAMEBUFFER_ATTACHMENT_BLUE_SIZE              = 0x8214;
    const GLenum FRAMEBUFFER_ATTACHMENT_ALPHA_SIZE             = 0x8215;
    const GLenum FRAMEBUFFER_ATTACHMENT_DEPTH_SIZE             = 0x8216;
    const GLenum FRAMEBUFFER_ATTACHMENT_STENCIL_SIZE           = 0x8217;
    const GLenum FRAMEBUFFER_DEFAULT                           = 0x8218;
    const GLenum DEPTH_STENCIL_ATTACHMENT                      = 0x821A;
    const GLenum DEPTH_STENCIL                                 = 0x84F9;
    const GLenum UNSIGNED_INT_24_8                             = 0x84FA;
    const GLenum DEPTH24_STENCIL8                              = 0x88F0;
    const GLenum UNSIGNED_NORMALIZED                           = 0x8C17;
    const GLenum DRAW_FRAMEBUFFER_BINDING                      = 0x8CA6; 
    const GLenum READ_FRAMEBUFFER                              = 0x8CA8;
    const GLenum DRAW_FRAMEBUFFER                              = 0x8CA9;
    const GLenum READ_FRAMEBUFFER_BINDING                      = 0x8CAA;
    const GLenum RENDERBUFFER_SAMPLES                          = 0x8CAB;
    const GLenum FRAMEBUFFER_ATTACHMENT_TEXTURE_LAYER          = 0x8CD4;
    const GLenum MAX_COLOR_ATTACHMENTS                         = 0x8CDF;
    const GLenum COLOR_ATTACHMENT1                             = 0x8CE1;
    const GLenum COLOR_ATTACHMENT2                             = 0x8CE2;
    const GLenum COLOR_ATTACHMENT3                             = 0x8CE3;
    const GLenum COLOR_ATTACHMENT4                             = 0x8CE4;
    const GLenum COLOR_ATTACHMENT5                             = 0x8CE5;
    const GLenum COLOR_ATTACHMENT6                             = 0x8CE6;
    const GLenum COLOR_ATTACHMENT7                             = 0x8CE7;
    const GLenum COLOR_ATTACHMENT8                             = 0x8CE8;
    const GLenum COLOR_ATTACHMENT9                             = 0x8CE9;
    const GLenum COLOR_ATTACHMENT10                            = 0x8CEA;
    const GLenum COLOR_ATTACHMENT11                            = 0x8CEB;
    const GLenum COLOR_ATTACHMENT12                            = 0x8CEC;
    const GLenum COLOR_ATTACHMENT13                            = 0x8CED;
    const GLenum COLOR_ATTACHMENT14                            = 0x8CEE;
    const GLenum COLOR_ATTACHMENT15                            = 0x8CEF;
    const GLenum FRAMEBUFFER_INCOMPLETE_MULTISAMPLE            = 0x8D56;
    const GLenum MAX_SAMPLES                                   = 0x8D57;
    const GLenum HALF_FLOAT                                    = 0x140B;
    const GLenum RG                                            = 0x8227;
    const GLenum RG_INTEGER                                    = 0x8228;
    const GLenum R8                                            = 0x8229;
    const GLenum RG8                                           = 0x822B;
    const GLenum R16F                                          = 0x822D;
    const GLenum R32F                                          = 0x822E;
    const GLenum RG16F                                         = 0x822F;
    const GLenum RG32F                                         = 0x8230;
    const GLenum R8I                                           = 0x8231;
    const GLenum R8UI                                          = 0x8232;
    const GLenum R16I                                          = 0x8233;
    const GLenum R16UI                                         = 0x8234;
    const GLenum R32I                                          = 0x8235;
    const GLenum R32UI                                         = 0x8236;
    const GLenum RG8I                                          = 0x8237;
    const GLenum RG8UI                                         = 0x8238;
    const GLenum RG16I                                         = 0x8239;
    const GLenum RG16UI                                        = 0x823A;
    const GLenum RG32I                                         = 0x823B;
    const GLenum RG32UI                                        = 0x823C;
    const GLenum VERTEX_ARRAY_BINDING                          = 0x85B5;
    const GLenum R8_SNORM                                      = 0x8F94;
    const GLenum RG8_SNORM                                     = 0x8F95;
    const GLenum RGB8_SNORM                                    = 0x8F96;
    const GLenum RGBA8_SNORM                                   = 0x8F97;
    const GLenum SIGNED_NORMALIZED                             = 0x8F9C;
    const GLenum COPY_READ_BUFFER                              = 0x8F36;
    const GLenum COPY_WRITE_BUFFER                             = 0x8F37;
    const GLenum COPY_READ_BUFFER_BINDING                      = 0x8F36; 
    const GLenum COPY_WRITE_BUFFER_BINDING                     = 0x8F37; 
    const GLenum UNIFORM_BUFFER                                = 0x8A11;
    const GLenum UNIFORM_BUFFER_BINDING                        = 0x8A28;
    const GLenum UNIFORM_BUFFER_START                          = 0x8A29;
    const GLenum UNIFORM_BUFFER_SIZE                           = 0x8A2A;
    const GLenum MAX_VERTEX_UNIFORM_BLOCKS                     = 0x8A2B;
    const GLenum MAX_FRAGMENT_UNIFORM_BLOCKS                   = 0x8A2D;
    const GLenum MAX_COMBINED_UNIFORM_BLOCKS                   = 0x8A2E;
    const GLenum MAX_UNIFORM_BUFFER_BINDINGS                   = 0x8A2F;
    const GLenum MAX_UNIFORM_BLOCK_SIZE                        = 0x8A30;
    const GLenum MAX_COMBINED_VERTEX_UNIFORM_COMPONENTS        = 0x8A31;
    const GLenum MAX_COMBINED_FRAGMENT_UNIFORM_COMPONENTS      = 0x8A33;
    const GLenum UNIFORM_BUFFER_OFFSET_ALIGNMENT               = 0x8A34;
    const GLenum ACTIVE_UNIFORM_BLOCKS                         = 0x8A36;
    const GLenum UNIFORM_TYPE                                  = 0x8A37;
    const GLenum UNIFORM_SIZE                                  = 0x8A38;
    const GLenum UNIFORM_BLOCK_INDEX                           = 0x8A3A;
    const GLenum UNIFORM_OFFSET                                = 0x8A3B;
    const GLenum UNIFORM_ARRAY_STRIDE                          = 0x8A3C;
    const GLenum UNIFORM_MATRIX_STRIDE                         = 0x8A3D;
    const GLenum UNIFORM_IS_ROW_MAJOR                          = 0x8A3E;
    const GLenum UNIFORM_BLOCK_BINDING                         = 0x8A3F;
    const GLenum UNIFORM_BLOCK_DATA_SIZE                       = 0x8A40;
    const GLenum UNIFORM_BLOCK_ACTIVE_UNIFORMS                 = 0x8A42;
    const GLenum UNIFORM_BLOCK_ACTIVE_UNIFORM_INDICES          = 0x8A43;
    const GLenum UNIFORM_BLOCK_REFERENCED_BY_VERTEX_SHADER     = 0x8A44;
    const GLenum UNIFORM_BLOCK_REFERENCED_BY_FRAGMENT_SHADER   = 0x8A46;
    const GLenum INVALID_INDEX                                 = 0xFFFFFFFF;
    const GLenum MAX_VERTEX_OUTPUT_COMPONENTS                  = 0x9122;
    const GLenum MAX_FRAGMENT_INPUT_COMPONENTS                 = 0x9125;
    const GLenum MAX_SERVER_WAIT_TIMEOUT                       = 0x9111;
    const GLenum OBJECT_TYPE                                   = 0x9112;
    const GLenum SYNC_CONDITION                                = 0x9113;
    const GLenum SYNC_STATUS                                   = 0x9114;
    const GLenum SYNC_FLAGS                                    = 0x9115;
    const GLenum SYNC_FENCE                                    = 0x9116;
    const GLenum SYNC_GPU_COMMANDS_COMPLETE                    = 0x9117;
    const GLenum UNSIGNALED                                    = 0x9118;
    const GLenum SIGNALED                                      = 0x9119;
    const GLenum ALREADY_SIGNALED                              = 0x911A;
    const GLenum TIMEOUT_EXPIRED                               = 0x911B;
    const GLenum CONDITION_SATISFIED                           = 0x911C;
    const GLenum WAIT_FAILED                                   = 0x911D;
    const GLenum SYNC_FLUSH_COMMANDS_BIT                       = 0x00000001;
    const GLenum VERTEX_ATTRIB_ARRAY_DIVISOR                   = 0x88FE;
    const GLenum ANY_SAMPLES_PASSED                            = 0x8C2F;
    const GLenum ANY_SAMPLES_PASSED_CONSERVATIVE               = 0x8D6A;
    const GLenum SAMPLER_BINDING                               = 0x8919;
    const GLenum RGB10_A2UI                                    = 0x906F;
    const GLenum INT_2_10_10_10_REV                            = 0x8D9F;
    const GLenum TRANSFORM_FEEDBACK                            = 0x8E22;
    const GLenum TRANSFORM_FEEDBACK_PAUSED                     = 0x8E23;
    const GLenum TRANSFORM_FEEDBACK_ACTIVE                     = 0x8E24;
    const GLenum TRANSFORM_FEEDBACK_BINDING                    = 0x8E25;
    const GLenum TEXTURE_IMMUTABLE_FORMAT                      = 0x912F;
    const GLenum MAX_ELEMENT_INDEX                             = 0x8D6B;
    const GLenum TEXTURE_IMMUTABLE_LEVELS                      = 0x82DF;

    const GLint64 TIMEOUT_IGNORED                              = -1;

    
    const GLenum MAX_CLIENT_WAIT_TIMEOUT_WEBGL                 = 0x9247;

    
    undefined bufferData(GLenum target, [AllowShared] ArrayBufferView data, GLenum usage, GLuint srcOffset, optional GLuint length = 0);
    undefined bufferSubData(GLenum target, GLintptr dstByteOffset, [AllowShared] ArrayBufferView srcData, GLuint srcOffset, optional GLuint length = 0);

    undefined copyBufferSubData(GLenum readTarget, GLenum writeTarget, GLintptr readOffset, GLintptr writeOffset, GLsizeiptr size);
    undefined getBufferSubData(GLenum target, GLintptr srcByteOffset, [AllowShared] ArrayBufferView dstData, optional GLuint dstOffset = 0, optional GLuint length = 0);

    
    undefined blitFramebuffer(GLint srcX0, GLint srcY0, GLint srcX1, GLint srcY1, GLint dstX0, GLint dstY0, GLint dstX1, GLint dstY1, GLbitfield mask, GLenum filter);
    undefined framebufferTextureLayer(GLenum target, GLenum attachment, WebGLTexture? texture, GLint level, GLint layer);
    undefined invalidateFramebuffer(GLenum target, sequence<GLenum> attachments);
    undefined invalidateSubFramebuffer(GLenum target, sequence<GLenum> attachments, GLint x, GLint y, GLsizei width, GLsizei height);
    undefined readBuffer(GLenum src);

    
    [OverrideIDLType=IDLWebGLAny] any getInternalformatParameter(GLenum target, GLenum internalformat, GLenum pname);
    undefined renderbufferStorageMultisample(GLenum target, GLsizei samples, GLenum internalformat, GLsizei width, GLsizei height);

    
    undefined texStorage2D(GLenum target, GLsizei levels, GLenum internalformat, GLsizei width, GLsizei height);
    undefined texStorage3D(GLenum target, GLsizei levels, GLenum internalformat, GLsizei width, GLsizei height, GLsizei depth);

    undefined texImage3D(GLenum target, GLint level, GLint internalformat, GLsizei width, GLsizei height, GLsizei depth, GLint border, GLenum format, GLenum type, GLintptr pboOffset);
    undefined texImage3D(GLenum target, GLint level, GLint internalformat, GLsizei width, GLsizei height, GLsizei depth, GLint border, GLenum format, GLenum type, TexImageSource source);
    undefined texImage3D(GLenum target, GLint level, GLint internalformat, GLsizei width, GLsizei height, GLsizei depth, GLint border, GLenum format, GLenum type, [AllowShared] ArrayBufferView? pixels);
    undefined texImage3D(GLenum target, GLint level, GLint internalformat, GLsizei width, GLsizei height, GLsizei depth, GLint border, GLenum format, GLenum type, [AllowShared] ArrayBufferView srcData, GLuint srcOffset);

    undefined texSubImage3D(GLenum target, GLint level, GLint xoffset, GLint yoffset, GLint zoffset, GLsizei width, GLsizei height, GLsizei depth, GLenum format, GLenum type, GLintptr pboOffset);
    undefined texSubImage3D(GLenum target, GLint level, GLint xoffset, GLint yoffset, GLint zoffset, GLsizei width, GLsizei height, GLsizei depth, GLenum format, GLenum type, TexImageSource source);
    undefined texSubImage3D(GLenum target, GLint level, GLint xoffset, GLint yoffset, GLint zoffset, GLsizei width, GLsizei height, GLsizei depth, GLenum format, GLenum type, [AllowShared] ArrayBufferView? srcData, optional GLuint srcOffset = 0);

    undefined copyTexSubImage3D(GLenum target, GLint level, GLint xoffset, GLint yoffset, GLint zoffset, GLint x, GLint y, GLsizei width, GLsizei height);

    undefined compressedTexImage3D(GLenum target, GLint level, GLenum internalformat, GLsizei width, GLsizei height, GLsizei depth, GLint border, GLsizei imageSize, GLintptr offset);
    undefined compressedTexImage3D(GLenum target, GLint level, GLenum internalformat, GLsizei width, GLsizei height, GLsizei depth, GLint border, [AllowShared] ArrayBufferView srcData, optional GLuint srcOffset = 0, optional GLuint srcLengthOverride = 0);
    undefined compressedTexSubImage3D(GLenum target, GLint level, GLint xoffset, GLint yoffset, GLint zoffset, GLsizei width, GLsizei height, GLsizei depth, GLenum format, GLsizei imageSize, GLintptr offset);
    undefined compressedTexSubImage3D(GLenum target, GLint level, GLint xoffset, GLint yoffset, GLint zoffset, GLsizei width, GLsizei height, GLsizei depth, GLenum format, [AllowShared] ArrayBufferView srcData, optional GLuint srcOffset = 0, optional GLuint srcLengthOverride = 0);

    
    GLint getFragDataLocation(WebGLProgram program, DOMString name);

    
    undefined uniform1ui(WebGLUniformLocation? location, GLuint v0);
    undefined uniform2ui(WebGLUniformLocation? location, GLuint v0, GLuint v1);
    undefined uniform3ui(WebGLUniformLocation? location, GLuint v0, GLuint v1, GLuint v2);
    undefined uniform4ui(WebGLUniformLocation? location, GLuint v0, GLuint v1, GLuint v2, GLuint v3);
    undefined uniform1uiv(WebGLUniformLocation? location, Uint32List data, optional GLuint srcOffset = 0, optional GLuint srcLength = 0);
    undefined uniform2uiv(WebGLUniformLocation? location, Uint32List data, optional GLuint srcOffset = 0, optional GLuint srcLength = 0);
    undefined uniform3uiv(WebGLUniformLocation? location, Uint32List data, optional GLuint srcOffset = 0, optional GLuint srcLength = 0);
    undefined uniform4uiv(WebGLUniformLocation? location, Uint32List data, optional GLuint srcOffset = 0, optional GLuint srcLength = 0);
    undefined uniformMatrix2x3fv(WebGLUniformLocation? location, GLboolean transpose, Float32List data, optional GLuint srcOffset = 0, optional GLuint srcLength = 0);
    undefined uniformMatrix3x2fv(WebGLUniformLocation? location, GLboolean transpose, Float32List data, optional GLuint srcOffset = 0, optional GLuint srcLength = 0);
    undefined uniformMatrix2x4fv(WebGLUniformLocation? location, GLboolean transpose, Float32List data, optional GLuint srcOffset = 0, optional GLuint srcLength = 0);
    undefined uniformMatrix4x2fv(WebGLUniformLocation? location, GLboolean transpose, Float32List data, optional GLuint srcOffset = 0, optional GLuint srcLength = 0);
    undefined uniformMatrix3x4fv(WebGLUniformLocation? location, GLboolean transpose, Float32List data, optional GLuint srcOffset = 0, optional GLuint srcLength = 0);
    undefined uniformMatrix4x3fv(WebGLUniformLocation? location, GLboolean transpose, Float32List data, optional GLuint srcOffset = 0, optional GLuint srcLength = 0);
    undefined vertexAttribI4i(GLuint index, GLint x, GLint y, GLint z, GLint w);
    undefined vertexAttribI4iv(GLuint index, Int32List values);
    undefined vertexAttribI4ui(GLuint index, GLuint x, GLuint y, GLuint z, GLuint w);
    undefined vertexAttribI4uiv(GLuint index, Uint32List values);
    undefined vertexAttribIPointer(GLuint index, GLint size, GLenum type, GLsizei stride, GLintptr offset);

    
    undefined vertexAttribDivisor(GLuint index, GLuint divisor);
    undefined drawArraysInstanced(GLenum mode, GLint first, GLsizei count, GLsizei instanceCount);
    undefined drawElementsInstanced(GLenum mode, GLsizei count, GLenum type, GLintptr offset, GLsizei instanceCount);
    undefined drawRangeElements(GLenum mode, GLuint start, GLuint end, GLsizei count, GLenum type, GLintptr offset);

    
    undefined drawBuffers(sequence<GLenum> buffers);
    undefined clearBufferiv(GLenum buffer, GLint drawbuffer, Int32List values, optional GLuint srcOffset = 0);
    undefined clearBufferuiv(GLenum buffer, GLint drawbuffer, Uint32List values, optional GLuint srcOffset = 0);
    undefined clearBufferfv(GLenum buffer, GLint drawbuffer, Float32List values, optional GLuint srcOffset = 0);
    undefined clearBufferfi(GLenum buffer, GLint drawbuffer, GLfloat depth, GLint stencil);

    
    WebGLQuery createQuery();
    undefined deleteQuery(WebGLQuery? query);
    GLboolean isQuery(WebGLQuery? query);
    undefined beginQuery(GLenum target, WebGLQuery query);
    [CallWith=CurrentScriptExecutionContext] undefined endQuery(GLenum target);
    [OverrideIDLType=IDLWebGLAny] any getQuery(GLenum target, GLenum pname);
    [OverrideIDLType=IDLWebGLAny] any getQueryParameter(WebGLQuery query, GLenum pname);

    
    WebGLSampler createSampler();
    undefined deleteSampler(WebGLSampler? sampler);
    GLboolean isSampler(WebGLSampler? sampler);
    undefined bindSampler(GLuint unit, WebGLSampler? sampler);
    undefined samplerParameteri(WebGLSampler sampler, GLenum pname, GLint param);
    undefined samplerParameterf(WebGLSampler sampler, GLenum pname, GLfloat param);
    [OverrideIDLType=IDLWebGLAny] any getSamplerParameter(WebGLSampler sampler, GLenum pname);

    
    WebGLSync? fenceSync(GLenum condition, GLbitfield flags);
    GLboolean isSync(WebGLSync? sync);
    undefined deleteSync(WebGLSync? sync);
    GLenum clientWaitSync(WebGLSync sync, GLbitfield flags, GLuint64 timeout);
    undefined waitSync(WebGLSync sync, GLbitfield flags, GLint64 timeout);
    [OverrideIDLType=IDLWebGLAny] any getSyncParameter(WebGLSync sync, GLenum pname);

    
    WebGLTransformFeedback createTransformFeedback();
    undefined deleteTransformFeedback(WebGLTransformFeedback? id);
    GLboolean isTransformFeedback(WebGLTransformFeedback? id);
    undefined bindTransformFeedback(GLenum target, WebGLTransformFeedback? id);
    undefined beginTransformFeedback(GLenum primitiveMode);
    undefined endTransformFeedback();
    undefined transformFeedbackVaryings(WebGLProgram program, sequence<DOMString> varyings, GLenum bufferMode);
    WebGLActiveInfo? getTransformFeedbackVarying(WebGLProgram program, GLuint index);
    undefined pauseTransformFeedback();
    undefined resumeTransformFeedback();

    
    undefined bindBufferBase(GLenum target, GLuint index, WebGLBuffer? buffer);
    undefined bindBufferRange(GLenum target, GLuint index, WebGLBuffer? buffer, GLintptr offset, GLsizeiptr size);
    [OverrideIDLType=IDLWebGLAny] any getIndexedParameter(GLenum target, GLuint index);
    sequence<GLuint>? getUniformIndices(WebGLProgram program, sequence<DOMString> uniformNames);
    [OverrideIDLType=IDLWebGLAny] any getActiveUniforms(WebGLProgram program, sequence<GLuint> uniformIndices, GLenum pname);
    GLuint getUniformBlockIndex(WebGLProgram program, DOMString uniformBlockName);
    [OverrideIDLType=IDLWebGLAny] any getActiveUniformBlockParameter(WebGLProgram program, GLuint uniformBlockIndex, GLenum pname);
    DOMString? getActiveUniformBlockName(WebGLProgram program, GLuint uniformBlockIndex);
    undefined uniformBlockBinding(WebGLProgram program, GLuint uniformBlockIndex, GLuint uniformBlockBinding);

    
    WebGLVertexArrayObject createVertexArray();
    undefined deleteVertexArray(WebGLVertexArrayObject? vertexArray);
    GLboolean isVertexArray(WebGLVertexArrayObject? vertexArray);
    undefined bindVertexArray(WebGLVertexArrayObject? vertexArray);

    

    undefined texImage2D(GLenum target, GLint level, GLint internalformat, GLsizei width, GLsizei height, GLint border, GLenum format, GLenum type, GLintptr pboOffset);
    undefined texImage2D(GLenum target, GLint level, GLint internalformat, GLsizei width, GLsizei height, GLint border, GLenum format, GLenum type, TexImageSource source);
    undefined texImage2D(GLenum target, GLint level, GLint internalformat, GLsizei width, GLsizei height, GLint border, GLenum format, GLenum type, [AllowShared] ArrayBufferView srcData, GLuint srcOffset);

    undefined texSubImage2D(GLenum target, GLint level, GLint xoffset, GLint yoffset, GLsizei width, GLsizei height, GLenum format, GLenum type, GLintptr pboOffset);
    undefined texSubImage2D(GLenum target, GLint level, GLint xoffset, GLint yoffset, GLsizei width, GLsizei height, GLenum format, GLenum type, TexImageSource source);
    undefined texSubImage2D(GLenum target, GLint level, GLint xoffset, GLint yoffset, GLsizei width, GLsizei height, GLenum format, GLenum type, [AllowShared] ArrayBufferView srcData, GLuint srcOffset);

    undefined compressedTexImage2D(GLenum target, GLint level, GLenum internalformat, GLsizei width, GLsizei height, GLint border, GLsizei imageSize, GLintptr offset);
    undefined compressedTexImage2D(GLenum target, GLint level, GLenum internalformat, GLsizei width, GLsizei height, GLint border, [AllowShared] ArrayBufferView srcData, GLuint srcOffset, optional GLuint srcLengthOverride = 0);

    undefined compressedTexSubImage2D(GLenum target, GLint level, GLint xoffset, GLint yoffset, GLsizei width, GLsizei height, GLenum format, GLsizei imageSize, GLintptr offset);
    undefined compressedTexSubImage2D(GLenum target, GLint level, GLint xoffset, GLint yoffset, GLsizei width, GLsizei height, GLenum format, [AllowShared] ArrayBufferView srcData, GLuint srcOffset, optional GLuint srcLengthOverride = 0);

    undefined uniform1fv(WebGLUniformLocation? location, Float32List data, GLuint srcOffset, optional GLuint srcLength = 0);
    undefined uniform2fv(WebGLUniformLocation? location, Float32List data, GLuint srcOffset, optional GLuint srcLength = 0);
    undefined uniform3fv(WebGLUniformLocation? location, Float32List data, GLuint srcOffset, optional GLuint srcLength = 0);
    undefined uniform4fv(WebGLUniformLocation? location, Float32List data, GLuint srcOffset, optional GLuint srcLength = 0);

    undefined uniform1iv(WebGLUniformLocation? location, Int32List data, GLuint srcOffset, optional GLuint srcLength = 0);
    undefined uniform2iv(WebGLUniformLocation? location, Int32List data, GLuint srcOffset, optional GLuint srcLength = 0);
    undefined uniform3iv(WebGLUniformLocation? location, Int32List data, GLuint srcOffset, optional GLuint srcLength = 0);
    undefined uniform4iv(WebGLUniformLocation? location, Int32List data, GLuint srcOffset, optional GLuint srcLength = 0);

    undefined uniformMatrix2fv(WebGLUniformLocation? location, GLboolean transpose, Float32List data, GLuint srcOffset, optional GLuint srcLength = 0);
    undefined uniformMatrix3fv(WebGLUniformLocation? location, GLboolean transpose, Float32List data, GLuint srcOffset, optional GLuint srcLength = 0);
    undefined uniformMatrix4fv(WebGLUniformLocation? location, GLboolean transpose, Float32List data, GLuint srcOffset, optional GLuint srcLength = 0);

    undefined readPixels(GLint x, GLint y, GLsizei width, GLsizei height, GLenum format, GLenum type, GLintptr offset);
    undefined readPixels(GLint x, GLint y, GLsizei width, GLsizei height, GLenum format, GLenum type, [AllowShared] ArrayBufferView dstData, GLuint dstOffset);
};

WebGL2RenderingContext includes WebGLRenderingContextBase;
```

### html/canvas/WebGLRenderingContext.idl

```webidl
typedef unsigned long GLenum;
typedef long GLint;
typedef long GLsizei;

typedef (ImageBitmap or ImageData or HTMLImageElement or HTMLCanvasElement
#if defined(ENABLE_VIDEO) && ENABLE_VIDEO
    or HTMLVideoElement
#endif
#if defined(ENABLE_OFFSCREEN_CANVAS) && ENABLE_OFFSCREEN_CANVAS
    or OffscreenCanvas
#endif
#if defined(ENABLE_WEB_CODECS) && ENABLE_WEB_CODECS
    or WebCodecsVideoFrame
#endif
) TexImageSource;

[
    ActiveDOMObject,
    Conditional=WEBGL,
    EnabledBySetting=WebGLEnabled,
    GenerateIsReachable=ImplCanvasBase,
    JSCustomMarkFunction,
    DoNotCheckConstants,
    ExportMacro=WEBCORE_EXPORT,
    ReportExtraMemoryCost,
    ReportExternalMemoryCost,
    Exposed=(Window,Worker),
    CallTracer=InspectorCanvasCallTracer,
] interface WebGLRenderingContext {
};

WebGLRenderingContext includes WebGLRenderingContextBase;
```

### html/canvas/WebGLRenderingContextBase.idl

```webidl
typedef unsigned long GLenum;
typedef boolean GLboolean;
typedef unsigned long GLbitfield;
typedef byte GLbyte; 
typedef short GLshort;
typedef long GLint;
typedef long GLsizei;
typedef long long GLintptr;
typedef long long GLsizeiptr;
typedef octet GLubyte; 
typedef unsigned short GLushort;
typedef unsigned long GLuint;
typedef unrestricted float GLfloat;
typedef unrestricted float GLclampf;
typedef (ArrayBuffer or ArrayBufferView) BufferDataSource;


typedef ([AllowShared] ArrayBuffer or [AllowShared] ArrayBufferView) AllowSharedBufferDataSource;
typedef ([AllowShared] Float32Array or sequence<GLfloat>) Float32List;
typedef ([AllowShared] Int32Array or sequence<GLint>) Int32List;

#if defined(ENABLE_OFFSCREEN_CANVAS)
typedef (HTMLCanvasElement or OffscreenCanvas) WebGLCanvas;
#else
typedef (HTMLCanvasElement) WebGLCanvas;
#endif

[
    Conditional=WEBGL,
    CustomIsReachable,
    JSCustomMarkFunction,
    ExportMacro=WEBCORE_EXPORT,
] interface mixin WebGLRenderingContextBase {

    readonly attribute WebGLCanvas canvas;

    
    const GLenum DEPTH_BUFFER_BIT = 0x00000100;
    const GLenum STENCIL_BUFFER_BIT = 0x00000400;
    const GLenum COLOR_BUFFER_BIT = 0x00004000;

    
    const GLenum POINTS = 0x0000;
    const GLenum LINES = 0x0001;
    const GLenum LINE_LOOP = 0x0002;
    const GLenum LINE_STRIP = 0x0003;
    const GLenum TRIANGLES = 0x0004;
    const GLenum TRIANGLE_STRIP = 0x0005;
    const GLenum TRIANGLE_FAN = 0x0006;

    
    
    
    
    
    
    
    
    

    
    const GLenum ZERO = 0;
    const GLenum ONE = 1;
    const GLenum SRC_COLOR = 0x0300;
    const GLenum ONE_MINUS_SRC_COLOR = 0x0301;
    const GLenum SRC_ALPHA = 0x0302;
    const GLenum ONE_MINUS_SRC_ALPHA = 0x0303;
    const GLenum DST_ALPHA = 0x0304;
    const GLenum ONE_MINUS_DST_ALPHA = 0x0305;

    
    
    
    const GLenum DST_COLOR = 0x0306;
    const GLenum ONE_MINUS_DST_COLOR = 0x0307;
    const GLenum SRC_ALPHA_SATURATE = 0x0308;
    
    
    
    

    
    const GLenum FUNC_ADD = 0x8006;
    const GLenum BLEND_EQUATION = 0x8009;
    const GLenum BLEND_EQUATION_RGB = 0x8009;   
    const GLenum BLEND_EQUATION_ALPHA = 0x883D;

    
    const GLenum FUNC_SUBTRACT = 0x800A;
    const GLenum FUNC_REVERSE_SUBTRACT = 0x800B;

    
    const GLenum BLEND_DST_RGB = 0x80C8;
    const GLenum BLEND_SRC_RGB = 0x80C9;
    const GLenum BLEND_DST_ALPHA = 0x80CA;
    const GLenum BLEND_SRC_ALPHA = 0x80CB;
    const GLenum CONSTANT_COLOR = 0x8001;
    const GLenum ONE_MINUS_CONSTANT_COLOR = 0x8002;
    const GLenum CONSTANT_ALPHA = 0x8003;
    const GLenum ONE_MINUS_CONSTANT_ALPHA = 0x8004;
    const GLenum BLEND_COLOR = 0x8005;

    
    const GLenum ARRAY_BUFFER = 0x8892;
    const GLenum ELEMENT_ARRAY_BUFFER = 0x8893;
    const GLenum ARRAY_BUFFER_BINDING = 0x8894;
    const GLenum ELEMENT_ARRAY_BUFFER_BINDING = 0x8895;

    const GLenum STREAM_DRAW = 0x88E0;
    const GLenum STATIC_DRAW = 0x88E4;
    const GLenum DYNAMIC_DRAW = 0x88E8;

    const GLenum BUFFER_SIZE = 0x8764;
    const GLenum BUFFER_USAGE = 0x8765;

    const GLenum CURRENT_VERTEX_ATTRIB = 0x8626;

    
    const GLenum FRONT = 0x0404;
    const GLenum BACK = 0x0405;
    const GLenum FRONT_AND_BACK = 0x0408;

    
    
    
    
    
    
    
    
    

    
    const GLenum TEXTURE_2D = 0x0DE1;
    const GLenum CULL_FACE = 0x0B44;
    const GLenum BLEND = 0x0BE2;
    const GLenum DITHER = 0x0BD0;
    const GLenum STENCIL_TEST = 0x0B90;
    const GLenum DEPTH_TEST = 0x0B71;
    const GLenum SCISSOR_TEST = 0x0C11;
    const GLenum POLYGON_OFFSET_FILL = 0x8037;
    const GLenum SAMPLE_ALPHA_TO_COVERAGE = 0x809E;
    const GLenum SAMPLE_COVERAGE = 0x80A0;

    
    const GLenum NO_ERROR = 0;
    const GLenum INVALID_ENUM = 0x0500;
    const GLenum INVALID_VALUE = 0x0501;
    const GLenum INVALID_OPERATION = 0x0502;
    const GLenum OUT_OF_MEMORY = 0x0505;

    
    const GLenum CW = 0x0900;
    const GLenum CCW = 0x0901;

    
    const GLenum LINE_WIDTH = 0x0B21;
    const GLenum ALIASED_POINT_SIZE_RANGE = 0x846D;
    const GLenum ALIASED_LINE_WIDTH_RANGE = 0x846E;
    const GLenum CULL_FACE_MODE = 0x0B45;
    const GLenum FRONT_FACE = 0x0B46;
    const GLenum DEPTH_RANGE = 0x0B70;
    const GLenum DEPTH_WRITEMASK = 0x0B72;
    const GLenum DEPTH_CLEAR_VALUE = 0x0B73;
    const GLenum DEPTH_FUNC = 0x0B74;
    const GLenum STENCIL_CLEAR_VALUE = 0x0B91;
    const GLenum STENCIL_FUNC = 0x0B92;
    const GLenum STENCIL_FAIL = 0x0B94;
    const GLenum STENCIL_PASS_DEPTH_FAIL = 0x0B95;
    const GLenum STENCIL_PASS_DEPTH_PASS = 0x0B96;
    const GLenum STENCIL_REF = 0x0B97;
    const GLenum STENCIL_VALUE_MASK = 0x0B93;
    const GLenum STENCIL_WRITEMASK = 0x0B98;
    const GLenum STENCIL_BACK_FUNC = 0x8800;
    const GLenum STENCIL_BACK_FAIL = 0x8801;
    const GLenum STENCIL_BACK_PASS_DEPTH_FAIL = 0x8802;
    const GLenum STENCIL_BACK_PASS_DEPTH_PASS = 0x8803;
    const GLenum STENCIL_BACK_REF = 0x8CA3;
    const GLenum STENCIL_BACK_VALUE_MASK = 0x8CA4;
    const GLenum STENCIL_BACK_WRITEMASK = 0x8CA5;
    const GLenum VIEWPORT = 0x0BA2;
    const GLenum SCISSOR_BOX = 0x0C10;
    
    const GLenum COLOR_CLEAR_VALUE = 0x0C22;
    const GLenum COLOR_WRITEMASK = 0x0C23;
    const GLenum UNPACK_ALIGNMENT = 0x0CF5;
    const GLenum PACK_ALIGNMENT = 0x0D05;
    const GLenum MAX_TEXTURE_SIZE = 0x0D33;
    const GLenum MAX_VIEWPORT_DIMS = 0x0D3A;
    const GLenum SUBPIXEL_BITS = 0x0D50;
    const GLenum RED_BITS = 0x0D52;
    const GLenum GREEN_BITS = 0x0D53;
    const GLenum BLUE_BITS = 0x0D54;
    const GLenum ALPHA_BITS = 0x0D55;
    const GLenum DEPTH_BITS = 0x0D56;
    const GLenum STENCIL_BITS = 0x0D57;
    const GLenum POLYGON_OFFSET_UNITS = 0x2A00;
    
    const GLenum POLYGON_OFFSET_FACTOR = 0x8038;
    const GLenum TEXTURE_BINDING_2D = 0x8069;
    const GLenum SAMPLE_BUFFERS = 0x80A8;
    const GLenum SAMPLES = 0x80A9;
    const GLenum SAMPLE_COVERAGE_VALUE = 0x80AA;
    const GLenum SAMPLE_COVERAGE_INVERT = 0x80AB;

    
    
    
    
    

    const GLenum COMPRESSED_TEXTURE_FORMATS = 0x86A3;

    
    const GLenum DONT_CARE = 0x1100;
    const GLenum FASTEST = 0x1101;
    const GLenum NICEST = 0x1102;

    
    const GLenum GENERATE_MIPMAP_HINT = 0x8192;

    
    const GLenum BYTE = 0x1400;
    const GLenum UNSIGNED_BYTE = 0x1401;
    const GLenum SHORT = 0x1402;
    const GLenum UNSIGNED_SHORT = 0x1403;
    const GLenum INT = 0x1404;
    const GLenum UNSIGNED_INT = 0x1405;
    const GLenum FLOAT = 0x1406;

    
    const GLenum DEPTH_COMPONENT = 0x1902;
    const GLenum ALPHA = 0x1906;
    const GLenum RGB = 0x1907;
    const GLenum RGBA = 0x1908;
    const GLenum LUMINANCE = 0x1909;
    const GLenum LUMINANCE_ALPHA = 0x190A;

    
    
    const GLenum UNSIGNED_SHORT_4_4_4_4 = 0x8033;
    const GLenum UNSIGNED_SHORT_5_5_5_1 = 0x8034;
    const GLenum UNSIGNED_SHORT_5_6_5 = 0x8363;

    
    const GLenum FRAGMENT_SHADER = 0x8B30;
    const GLenum VERTEX_SHADER = 0x8B31;
    const GLenum MAX_VERTEX_ATTRIBS = 0x8869;
    const GLenum MAX_VERTEX_UNIFORM_VECTORS = 0x8DFB;
    const GLenum MAX_VARYING_VECTORS = 0x8DFC;
    const GLenum MAX_COMBINED_TEXTURE_IMAGE_UNITS = 0x8B4D;
    const GLenum MAX_VERTEX_TEXTURE_IMAGE_UNITS = 0x8B4C;
    const GLenum MAX_TEXTURE_IMAGE_UNITS = 0x8872;
    const GLenum MAX_FRAGMENT_UNIFORM_VECTORS = 0x8DFD;
    const GLenum SHADER_TYPE = 0x8B4F;
    const GLenum DELETE_STATUS = 0x8B80;
    const GLenum LINK_STATUS = 0x8B82;
    const GLenum VALIDATE_STATUS = 0x8B83;
    const GLenum ATTACHED_SHADERS = 0x8B85;
    const GLenum ACTIVE_UNIFORMS = 0x8B86;
    const GLenum ACTIVE_ATTRIBUTES = 0x8B89;
    const GLenum SHADING_LANGUAGE_VERSION = 0x8B8C;
    const GLenum CURRENT_PROGRAM = 0x8B8D;

    
    const GLenum NEVER = 0x0200;
    const GLenum LESS = 0x0201;
    const GLenum EQUAL = 0x0202;
    const GLenum LEQUAL = 0x0203;
    const GLenum GREATER = 0x0204;
    const GLenum NOTEQUAL = 0x0205;
    const GLenum GEQUAL = 0x0206;
    const GLenum ALWAYS = 0x0207;

    
    
    const GLenum KEEP = 0x1E00;
    const GLenum REPLACE = 0x1E01;
    const GLenum INCR = 0x1E02;
    const GLenum DECR = 0x1E03;
    const GLenum INVERT = 0x150A;
    const GLenum INCR_WRAP = 0x8507;
    const GLenum DECR_WRAP = 0x8508;

    
    const GLenum VENDOR = 0x1F00;
    const GLenum RENDERER = 0x1F01;
    const GLenum VERSION = 0x1F02;

    
    const GLenum NEAREST = 0x2600;
    const GLenum LINEAR = 0x2601;

    
    
    
    const GLenum NEAREST_MIPMAP_NEAREST = 0x2700;
    const GLenum LINEAR_MIPMAP_NEAREST = 0x2701;
    const GLenum NEAREST_MIPMAP_LINEAR = 0x2702;
    const GLenum LINEAR_MIPMAP_LINEAR = 0x2703;

    
    const GLenum TEXTURE_MAG_FILTER = 0x2800;
    const GLenum TEXTURE_MIN_FILTER = 0x2801;
    const GLenum TEXTURE_WRAP_S = 0x2802;
    const GLenum TEXTURE_WRAP_T = 0x2803;

    
    
    const GLenum TEXTURE = 0x1702;

    const GLenum TEXTURE_CUBE_MAP = 0x8513;
    const GLenum TEXTURE_BINDING_CUBE_MAP = 0x8514;
    const GLenum TEXTURE_CUBE_MAP_POSITIVE_X = 0x8515;
    const GLenum TEXTURE_CUBE_MAP_NEGATIVE_X = 0x8516;
    const GLenum TEXTURE_CUBE_MAP_POSITIVE_Y = 0x8517;
    const GLenum TEXTURE_CUBE_MAP_NEGATIVE_Y = 0x8518;
    const GLenum TEXTURE_CUBE_MAP_POSITIVE_Z = 0x8519;
    const GLenum TEXTURE_CUBE_MAP_NEGATIVE_Z = 0x851A;
    const GLenum MAX_CUBE_MAP_TEXTURE_SIZE = 0x851C;

    
    const GLenum TEXTURE0 = 0x84C0;
    const GLenum TEXTURE1 = 0x84C1;
    const GLenum TEXTURE2 = 0x84C2;
    const GLenum TEXTURE3 = 0x84C3;
    const GLenum TEXTURE4 = 0x84C4;
    const GLenum TEXTURE5 = 0x84C5;
    const GLenum TEXTURE6 = 0x84C6;
    const GLenum TEXTURE7 = 0x84C7;
    const GLenum TEXTURE8 = 0x84C8;
    const GLenum TEXTURE9 = 0x84C9;
    const GLenum TEXTURE10 = 0x84CA;
    const GLenum TEXTURE11 = 0x84CB;
    const GLenum TEXTURE12 = 0x84CC;
    const GLenum TEXTURE13 = 0x84CD;
    const GLenum TEXTURE14 = 0x84CE;
    const GLenum TEXTURE15 = 0x84CF;
    const GLenum TEXTURE16 = 0x84D0;
    const GLenum TEXTURE17 = 0x84D1;
    const GLenum TEXTURE18 = 0x84D2;
    const GLenum TEXTURE19 = 0x84D3;
    const GLenum TEXTURE20 = 0x84D4;
    const GLenum TEXTURE21 = 0x84D5;
    const GLenum TEXTURE22 = 0x84D6;
    const GLenum TEXTURE23 = 0x84D7;
    const GLenum TEXTURE24 = 0x84D8;
    const GLenum TEXTURE25 = 0x84D9;
    const GLenum TEXTURE26 = 0x84DA;
    const GLenum TEXTURE27 = 0x84DB;
    const GLenum TEXTURE28 = 0x84DC;
    const GLenum TEXTURE29 = 0x84DD;
    const GLenum TEXTURE30 = 0x84DE;
    const GLenum TEXTURE31 = 0x84DF;
    const GLenum ACTIVE_TEXTURE = 0x84E0;

    
    const GLenum REPEAT = 0x2901;
    const GLenum CLAMP_TO_EDGE = 0x812F;
    const GLenum MIRRORED_REPEAT = 0x8370;

    
    const GLenum FLOAT_VEC2 = 0x8B50;
    const GLenum FLOAT_VEC3 = 0x8B51;
    const GLenum FLOAT_VEC4 = 0x8B52;
    const GLenum INT_VEC2 = 0x8B53;
    const GLenum INT_VEC3 = 0x8B54;
    const GLenum INT_VEC4 = 0x8B55;
    const GLenum BOOL = 0x8B56;
    const GLenum BOOL_VEC2 = 0x8B57;
    const GLenum BOOL_VEC3 = 0x8B58;
    const GLenum BOOL_VEC4 = 0x8B59;
    const GLenum FLOAT_MAT2 = 0x8B5A;
    const GLenum FLOAT_MAT3 = 0x8B5B;
    const GLenum FLOAT_MAT4 = 0x8B5C;
    const GLenum SAMPLER_2D = 0x8B5E;
    const GLenum SAMPLER_CUBE = 0x8B60;

    
    const GLenum VERTEX_ATTRIB_ARRAY_ENABLED = 0x8622;
    const GLenum VERTEX_ATTRIB_ARRAY_SIZE = 0x8623;
    const GLenum VERTEX_ATTRIB_ARRAY_STRIDE = 0x8624;
    const GLenum VERTEX_ATTRIB_ARRAY_TYPE = 0x8625;
    const GLenum VERTEX_ATTRIB_ARRAY_NORMALIZED = 0x886A;
    const GLenum VERTEX_ATTRIB_ARRAY_POINTER = 0x8645;
    const GLenum VERTEX_ATTRIB_ARRAY_BUFFER_BINDING = 0x889F;

    
    const GLenum IMPLEMENTATION_COLOR_READ_TYPE = 0x8B9A;
    const GLenum IMPLEMENTATION_COLOR_READ_FORMAT = 0x8B9B;

    
    const GLenum COMPILE_STATUS = 0x8B81;

    
    const GLenum LOW_FLOAT = 0x8DF0;
    const GLenum MEDIUM_FLOAT = 0x8DF1;
    const GLenum HIGH_FLOAT = 0x8DF2;
    const GLenum LOW_INT = 0x8DF3;
    const GLenum MEDIUM_INT = 0x8DF4;
    const GLenum HIGH_INT = 0x8DF5;

    
    const GLenum FRAMEBUFFER = 0x8D40;
    const GLenum RENDERBUFFER = 0x8D41;

    const GLenum RGBA4 = 0x8056;
    const GLenum RGB5_A1 = 0x8057;
    const GLenum RGB565 = 0x8D62;
    const GLenum DEPTH_COMPONENT16 = 0x81A5;
    const GLenum STENCIL_INDEX8 = 0x8D48;
    const GLenum DEPTH_STENCIL = 0x84F9;

    const GLenum RENDERBUFFER_WIDTH = 0x8D42;
    const GLenum RENDERBUFFER_HEIGHT = 0x8D43;
    const GLenum RENDERBUFFER_INTERNAL_FORMAT = 0x8D44;
    const GLenum RENDERBUFFER_RED_SIZE = 0x8D50;
    const GLenum RENDERBUFFER_GREEN_SIZE = 0x8D51;
    const GLenum RENDERBUFFER_BLUE_SIZE = 0x8D52;
    const GLenum RENDERBUFFER_ALPHA_SIZE = 0x8D53;
    const GLenum RENDERBUFFER_DEPTH_SIZE = 0x8D54;
    const GLenum RENDERBUFFER_STENCIL_SIZE = 0x8D55;

    const GLenum FRAMEBUFFER_ATTACHMENT_OBJECT_TYPE = 0x8CD0;
    const GLenum FRAMEBUFFER_ATTACHMENT_OBJECT_NAME = 0x8CD1;
    const GLenum FRAMEBUFFER_ATTACHMENT_TEXTURE_LEVEL = 0x8CD2;
    const GLenum FRAMEBUFFER_ATTACHMENT_TEXTURE_CUBE_MAP_FACE = 0x8CD3;

    const GLenum COLOR_ATTACHMENT0 = 0x8CE0;
    const GLenum DEPTH_ATTACHMENT = 0x8D00;
    const GLenum STENCIL_ATTACHMENT = 0x8D20;
    const GLenum DEPTH_STENCIL_ATTACHMENT = 0x821A;

    const GLenum NONE = 0;

    const GLenum FRAMEBUFFER_COMPLETE = 0x8CD5;
    const GLenum FRAMEBUFFER_INCOMPLETE_ATTACHMENT = 0x8CD6;
    const GLenum FRAMEBUFFER_INCOMPLETE_MISSING_ATTACHMENT = 0x8CD7;
    const GLenum FRAMEBUFFER_INCOMPLETE_DIMENSIONS = 0x8CD9;
    const GLenum FRAMEBUFFER_UNSUPPORTED = 0x8CDD;

    const GLenum FRAMEBUFFER_BINDING = 0x8CA6;
    const GLenum RENDERBUFFER_BINDING = 0x8CA7;
    const GLenum MAX_RENDERBUFFER_SIZE = 0x84E8;

    const GLenum INVALID_FRAMEBUFFER_OPERATION = 0x0506;

    
    const GLenum UNPACK_FLIP_Y_WEBGL = 0x9240;
    const GLenum UNPACK_PREMULTIPLY_ALPHA_WEBGL = 0x9241;
    const GLenum CONTEXT_LOST_WEBGL = 0x9242;
    const GLenum UNPACK_COLORSPACE_CONVERSION_WEBGL = 0x9243;
    const GLenum BROWSER_DEFAULT_WEBGL = 0x9244;

    readonly attribute GLsizei drawingBufferWidth;
    readonly attribute GLsizei drawingBufferHeight;

#if defined(ENABLE_PREDEFINED_COLOR_SPACE_DISPLAY_P3) && ENABLE_PREDEFINED_COLOR_SPACE_DISPLAY_P3
    [EnabledBySetting=CanvasColorSpaceEnabled] attribute PredefinedColorSpace drawingBufferColorSpace;
#endif

    undefined activeTexture(GLenum texture);
    undefined attachShader(WebGLProgram program, WebGLShader shader);
    undefined bindAttribLocation(WebGLProgram program, GLuint index, DOMString name);
    undefined bindBuffer(GLenum target, WebGLBuffer? buffer);
    undefined bindFramebuffer(GLenum target, WebGLFramebuffer? framebuffer);
    undefined bindRenderbuffer(GLenum target, WebGLRenderbuffer? renderbuffer);
    undefined bindTexture(GLenum target, WebGLTexture? texture);
    undefined blendColor(GLclampf red, GLclampf green, GLclampf blue, GLclampf alpha);
    undefined blendEquation(GLenum mode);
    undefined blendEquationSeparate(GLenum modeRGB, GLenum modeAlpha);
    undefined blendFunc(GLenum sfactor, GLenum dfactor);
    undefined blendFuncSeparate(GLenum srcRGB, GLenum dstRGB, GLenum srcAlpha, GLenum dstAlpha);
    undefined bufferData(GLenum target, AllowSharedBufferDataSource? data, GLenum usage);
    undefined bufferData(GLenum target, GLsizeiptr size, GLenum usage);
    undefined bufferSubData(GLenum target, GLintptr offset, AllowSharedBufferDataSource data);

    GLenum checkFramebufferStatus(GLenum target);
    undefined clear(GLbitfield mask);
    undefined clearColor(GLclampf red, GLclampf green, GLclampf blue, GLclampf alpha);
    undefined clearDepth(GLclampf depth);
    undefined clearStencil(GLint s);
    undefined colorMask(GLboolean red, GLboolean green, GLboolean blue, GLboolean alpha);
    undefined compileShader(WebGLShader shader);

    undefined texImage2D(GLenum target, GLint level, GLenum internalformat, GLsizei width, GLsizei height, GLint border, GLenum format, GLenum type, [AllowShared] ArrayBufferView? pixels);
    undefined texImage2D(GLenum target, GLint level, GLenum internalformat, GLenum format, GLenum type, TexImageSource source);

    undefined texSubImage2D(GLenum target, GLint level, GLint xoffset, GLint yoffset, GLsizei width, GLsizei height, GLenum format, GLenum type, [AllowShared] ArrayBufferView? pixels);
    undefined texSubImage2D(GLenum target, GLint level, GLint xoffset, GLint yoffset, GLenum format, GLenum type, TexImageSource source);

    undefined compressedTexImage2D(GLenum target, GLint level, GLenum internalformat, GLsizei width, GLsizei height, GLint border, [AllowShared] ArrayBufferView data);
    undefined compressedTexSubImage2D(GLenum target, GLint level, GLint xoffset, GLint yoffset, GLsizei width, GLsizei height, GLenum format, [AllowShared] ArrayBufferView data);

    undefined copyTexImage2D(GLenum target, GLint level, GLenum internalformat, GLint x, GLint y, GLsizei width, GLsizei height, GLint border);
    undefined copyTexSubImage2D(GLenum target, GLint level, GLint xoffset, GLint yoffset, GLint x, GLint y, GLsizei width, GLsizei height);

    WebGLBuffer createBuffer();
    WebGLFramebuffer createFramebuffer();
    WebGLProgram createProgram();
    WebGLRenderbuffer createRenderbuffer();
    WebGLShader? createShader(GLenum type);
    WebGLTexture createTexture();

    undefined cullFace(GLenum mode);

    undefined deleteBuffer(WebGLBuffer? buffer);
    undefined deleteFramebuffer(WebGLFramebuffer? framebuffer);
    undefined deleteProgram(WebGLProgram? program);
    undefined deleteRenderbuffer(WebGLRenderbuffer? renderbuffer);
    undefined deleteShader(WebGLShader? shader);
    undefined deleteTexture(WebGLTexture? texture);

    undefined depthFunc(GLenum func);
    undefined depthMask(GLboolean flag);
    undefined depthRange(GLclampf zNear, GLclampf zFar);
    undefined detachShader(WebGLProgram program, WebGLShader shader);
    undefined disable(GLenum cap);
    undefined disableVertexAttribArray(GLuint index);
    undefined drawArrays(GLenum mode, GLint first, GLsizei count);
    undefined drawElements(GLenum mode, GLsizei count, GLenum type, GLintptr offset);

    undefined enable(GLenum cap);
    undefined enableVertexAttribArray(GLuint index);
    undefined finish();
    undefined flush();
    undefined framebufferRenderbuffer(GLenum target, GLenum attachment, GLenum renderbuffertarget, WebGLRenderbuffer? renderbuffer);
    undefined framebufferTexture2D(GLenum target, GLenum attachment, GLenum textarget, WebGLTexture? texture, GLint level);
    undefined frontFace(GLenum mode);
    undefined generateMipmap(GLenum target);
    
    WebGLActiveInfo? getActiveAttrib(WebGLProgram program, GLuint index);
    WebGLActiveInfo? getActiveUniform(WebGLProgram program, GLuint index);

    
    sequence<WebGLShader>? getAttachedShaders(WebGLProgram program);

    GLint getAttribLocation(WebGLProgram program, DOMString name);

    [OverrideIDLType=IDLWebGLAny] any getBufferParameter(GLenum target, GLenum pname);

    WebGLContextAttributes? getContextAttributes();

    GLenum getError();

    sequence<DOMString>? getSupportedExtensions();
    [OverrideIDLType=IDLWebGLExtensionAny] object? getExtension(DOMString name);

    [OverrideIDLType=IDLWebGLAny] any getFramebufferAttachmentParameter(GLenum target, GLenum attachment, GLenum pname);
    [OverrideIDLType=IDLWebGLAny] any getParameter(GLenum pname);
    [OverrideIDLType=IDLWebGLAny] any getProgramParameter(WebGLProgram program, GLenum pname);
    DOMString? getProgramInfoLog(WebGLProgram program);
    [OverrideIDLType=IDLWebGLAny] any getRenderbufferParameter(GLenum target, GLenum pname);
    [OverrideIDLType=IDLWebGLAny] any getShaderParameter(WebGLShader shader, GLenum pname);

    DOMString? getShaderInfoLog(WebGLShader shader);

    WebGLShaderPrecisionFormat? getShaderPrecisionFormat(GLenum shadertype, GLenum precisiontype);

    DOMString? getShaderSource(WebGLShader shader);

    [OverrideIDLType=IDLWebGLAny] any getTexParameter(GLenum target, GLenum pname);

    [OverrideIDLType=IDLWebGLAny] any getUniform(WebGLProgram program, WebGLUniformLocation location);

    WebGLUniformLocation? getUniformLocation(WebGLProgram program, DOMString name);

    [OverrideIDLType=IDLWebGLAny] any getVertexAttrib(GLuint index, GLenum pname);

    GLsizeiptr getVertexAttribOffset(GLuint index, GLenum pname);

    undefined hint(GLenum target, GLenum mode);
    GLboolean isBuffer(WebGLBuffer? buffer);
    GLboolean isContextLost();
    GLboolean isEnabled(GLenum cap);
    GLboolean isFramebuffer(WebGLFramebuffer? framebuffer);
    GLboolean isProgram(WebGLProgram? program);
    GLboolean isRenderbuffer(WebGLRenderbuffer? renderbuffer);
    GLboolean isShader(WebGLShader? shader);
    GLboolean isTexture(WebGLTexture? texture);
    undefined lineWidth(GLfloat width);
    undefined linkProgram(WebGLProgram program);

    [NewObject, Conditional=WEBXR, EnabledBySetting=WebXREnabled] Promise<undefined> makeXRCompatible();

    undefined pixelStorei(GLenum pname, GLint param);
    undefined polygonOffset(GLfloat factor, GLfloat units);

    undefined readPixels(GLint x, GLint y, GLsizei width, GLsizei height, GLenum format, GLenum type, [AllowShared] ArrayBufferView? pixels);
    
    undefined renderbufferStorage(GLenum target, GLenum internalformat, GLsizei width, GLsizei height);
    undefined sampleCoverage(GLclampf value, GLboolean invert);
    undefined scissor(GLint x, GLint y, GLsizei width, GLsizei height);
    undefined shaderSource(WebGLShader shader, DOMString string);
    undefined stencilFunc(GLenum func, GLint ref, GLuint mask);
    undefined stencilFuncSeparate(GLenum face, GLenum func, GLint ref, GLuint mask);
    undefined stencilMask(GLuint mask);
    undefined stencilMaskSeparate(GLenum face, GLuint mask);
    undefined stencilOp(GLenum fail, GLenum zfail, GLenum zpass);
    undefined stencilOpSeparate(GLenum face, GLenum fail, GLenum zfail, GLenum zpass);

    undefined texParameterf(GLenum target, GLenum pname, GLfloat param);
    undefined texParameteri(GLenum target, GLenum pname, GLint param);

    undefined uniform1f(WebGLUniformLocation? location, GLfloat x);
    undefined uniform2f(WebGLUniformLocation? location, GLfloat x, GLfloat y);
    undefined uniform3f(WebGLUniformLocation? location, GLfloat x, GLfloat y, GLfloat z);
    undefined uniform4f(WebGLUniformLocation? location, GLfloat x, GLfloat y, GLfloat z, GLfloat w);

    undefined uniform1i(WebGLUniformLocation? location, GLint x);
    undefined uniform2i(WebGLUniformLocation? location, GLint x, GLint y);
    undefined uniform3i(WebGLUniformLocation? location, GLint x, GLint y, GLint z);
    undefined uniform4i(WebGLUniformLocation? location, GLint x, GLint y, GLint z, GLint w);

    undefined uniform1fv(WebGLUniformLocation? location, Float32List v);
    undefined uniform2fv(WebGLUniformLocation? location, Float32List v);
    undefined uniform3fv(WebGLUniformLocation? location, Float32List v);
    undefined uniform4fv(WebGLUniformLocation? location, Float32List v);

    undefined uniform1iv(WebGLUniformLocation? location, Int32List v);
    undefined uniform2iv(WebGLUniformLocation? location, Int32List v);
    undefined uniform3iv(WebGLUniformLocation? location, Int32List v);
    undefined uniform4iv(WebGLUniformLocation? location, Int32List v);

    undefined uniformMatrix2fv(WebGLUniformLocation? location, GLboolean transpose, Float32List array);
    undefined uniformMatrix3fv(WebGLUniformLocation? location, GLboolean transpose, Float32List array);
    undefined uniformMatrix4fv(WebGLUniformLocation? location, GLboolean transpose, Float32List array);

    undefined useProgram(WebGLProgram? program);
    undefined validateProgram(WebGLProgram program);

    undefined vertexAttrib1f(GLuint index, GLfloat x);
    undefined vertexAttrib2f(GLuint index, GLfloat x, GLfloat y);
    undefined vertexAttrib3f(GLuint index, GLfloat x, GLfloat y, GLfloat z);
    undefined vertexAttrib4f(GLuint index, GLfloat x, GLfloat y, GLfloat z, GLfloat w);

    undefined vertexAttrib1fv(GLuint index, Float32List values);
    undefined vertexAttrib2fv(GLuint index, Float32List values);
    undefined vertexAttrib3fv(GLuint index, Float32List values);
    undefined vertexAttrib4fv(GLuint index, Float32List values);

    undefined vertexAttribPointer(GLuint indx, GLint size, GLenum type, GLboolean normalized, GLsizei stride, GLintptr offset);

    undefined viewport(GLint x, GLint y, GLsizei width, GLsizei height);
};
```

### html/HTMLCanvasElement.idl

```webidl
typedef (
#if defined(ENABLE_WEBGL) && ENABLE_WEBGL
    WebGLRenderingContext or
    WebGL2RenderingContext or
#endif
    GPUCanvasContext or
    ImageBitmapRenderingContext or 
    CanvasRenderingContext2D) RenderingContext;

[
    ExportMacro=WEBCORE_EXPORT,
    ActiveDOMObject,
    JSCustomMarkFunction,
    JSGenerateToNativeObject,
    Exposed=Window
] interface HTMLCanvasElement : HTMLElement {
    [CEReactions=NotNeeded, CallTracer=InspectorCanvasCallTracer] attribute unsigned long width;
    [CEReactions=NotNeeded, CallTracer=InspectorCanvasCallTracer] attribute unsigned long height;

    [CallWith=CurrentGlobalObject] RenderingContext? getContext(DOMString contextId, any... arguments);

    DOMString toDataURL(optional DOMString type, optional any quality);
    undefined toBlob(BlobCallback callback, optional DOMString type, optional any quality);
    [Conditional=OFFSCREEN_CANVAS, EnabledBySetting=OffscreenCanvasEnabled, NewObject] OffscreenCanvas transferControlToOffscreen();

    [Conditional=MEDIA_STREAM, NewObject] MediaStream captureStream(optional double frameRequestRate);
};
```

### html/HTMLMediaElement.idl

```webidl
typedef (
#if defined(ENABLE_MEDIA_STREAM) && ENABLE_MEDIA_STREAM
    MediaStream or
#endif
#if defined(ENABLE_MEDIA_SOURCE) && ENABLE_MEDIA_SOURCE
    MediaSource or
#endif
#if defined(ENABLE_MEDIA_SOURCE_IN_WORKERS) && ENABLE_MEDIA_SOURCE_IN_WORKERS
    MediaSourceHandle or
#endif
    Blob) MediaProvider;

[
    ActiveDOMObject,
    Conditional=VIDEO,
    ExportMacro=WEBCORE_EXPORT,
    JSCustomMarkFunction,
    JSGenerateToNativeObject,
    Exposed=Window
] interface HTMLMediaElement : HTMLElement {

    
    readonly attribute MediaError? error;

    
    [CEReactions=NotNeeded, ReflectURL] attribute USVString src;
    attribute MediaProvider? srcObject;
    [URL] readonly attribute USVString currentSrc;
    [CEReactions=NotNeeded, ReflectSetter] attribute [AtomString] DOMString? crossOrigin;

    const unsigned short NETWORK_EMPTY = 0;
    const unsigned short NETWORK_IDLE = 1;
    const unsigned short NETWORK_LOADING = 2;
    const unsigned short NETWORK_NO_SOURCE = 3;
    readonly attribute unsigned short networkState;
    [CEReactions=NotNeeded] attribute [AtomString] DOMString preload;

    readonly attribute TimeRanges buffered;
    undefined load();

    
    DOMString canPlayType(DOMString type);

    
    const unsigned short HAVE_NOTHING = 0;
    const unsigned short HAVE_METADATA = 1;
    const unsigned short HAVE_CURRENT_DATA = 2;
    const unsigned short HAVE_FUTURE_DATA = 3;
    const unsigned short HAVE_ENOUGH_DATA = 4;
    readonly attribute unsigned short readyState;
    readonly attribute boolean seeking;

    
    
    [ImplementedAs=currentTimeForBindings] attribute unrestricted double currentTime;
    
    undefined fastSeek(unrestricted double time);
    readonly attribute unrestricted double duration;
    Date getStartDate();
    readonly attribute boolean paused;
    attribute double defaultPlaybackRate;
    attribute double playbackRate;
    [CEReactions=NotNeeded] attribute boolean preservesPitch;
    readonly attribute TimeRanges played;
    readonly attribute TimeRanges seekable;
    readonly attribute boolean ended;
    [CEReactions=NotNeeded, Reflect] attribute boolean autoplay;
    [CEReactions=NotNeeded, Reflect] attribute boolean loop;
    Promise<undefined> play();
    undefined pause();

    
    [CEReactions=NotNeeded] attribute boolean controls;
    attribute double volume;
    attribute boolean muted;
    [CEReactions=NotNeeded, Reflect="muted"] attribute boolean defaultMuted;

    
    [Conditional=MEDIA_STATISTICS] readonly attribute unsigned long webkitAudioDecodedByteCount;
    [Conditional=MEDIA_STATISTICS] readonly attribute unsigned long webkitVideoDecodedByteCount;

    [Conditional=LEGACY_ENCRYPTED_MEDIA, EnabledBySetting=LegacyEncryptedMediaAPIEnabled] readonly attribute WebKitMediaKeys? webkitKeys;
    [Conditional=LEGACY_ENCRYPTED_MEDIA, EnabledBySetting=LegacyEncryptedMediaAPIEnabled] undefined webkitSetMediaKeys(WebKitMediaKeys? mediaKeys);

    
    
    [Conditional=ENCRYPTED_MEDIA, EnabledBySetting=EncryptedMediaAPIEnabled, DisabledByQuirk=hasBrokenEncryptedMediaAPISupport] readonly attribute MediaKeys? mediaKeys;
    [Conditional=ENCRYPTED_MEDIA, EnabledBySetting=EncryptedMediaAPIEnabled, DisabledByQuirk=hasBrokenEncryptedMediaAPISupport] attribute EventHandler onencrypted;
    [Conditional=ENCRYPTED_MEDIA, EnabledBySetting=EncryptedMediaAPIEnabled, DisabledByQuirk=hasBrokenEncryptedMediaAPISupport] attribute EventHandler onwaitingforkey;
    [Conditional=ENCRYPTED_MEDIA, EnabledBySetting=EncryptedMediaAPIEnabled, DisabledByQuirk=hasBrokenEncryptedMediaAPISupport] Promise<undefined> setMediaKeys(MediaKeys? mediaKeys);

    
    TextTrack addTextTrack([AtomString] DOMString kind, optional [AtomString] DOMString label = "", optional [AtomString] DOMString language = "");
    [SameObject, ImplementedAs=ensureAudioTracks] readonly attribute AudioTrackList audioTracks;
    [SameObject, ImplementedAs=ensureTextTracks] readonly attribute TextTrackList textTracks;
    [SameObject, ImplementedAs=ensureVideoTracks] readonly attribute VideoTrackList videoTracks;

    [Reflect] attribute DOMString mediaGroup;

    [ImplementedAs=controllerForBindings] attribute MediaController? controller;

    VideoPlaybackQuality getVideoPlaybackQuality();

    [Conditional=WIRELESS_PLAYBACK_TARGET, EnabledBySetting=WirelessPlaybackTargetAPIEnabled] undefined webkitShowPlaybackTargetPicker();
    [Conditional=WIRELESS_PLAYBACK_TARGET, EnabledBySetting=WirelessPlaybackTargetAPIEnabled] readonly attribute boolean webkitCurrentPlaybackTargetIsWireless;

    
    [EnabledForWorld=isMediaControls, ImplementedAs=mediaControlsHost] readonly attribute MediaControlsHost? controlsHost;
};
```

### html/OffscreenCanvas.idl

```webidl
typedef (
#if defined(ENABLE_WEBGL) && ENABLE_WEBGL
    WebGLRenderingContext or
    WebGL2RenderingContext or
#endif
    GPUCanvasContext or
    ImageBitmapRenderingContext or
    OffscreenCanvasRenderingContext2D) OffscreenRenderingContext;


dictionary ImageEncodeOptions {
   DOMString type = "image/png";
   unrestricted double quality = 1.0;
};



enum OffscreenRenderingContextType {
   "2d",
   "webgl",
   "webgl2",
   "bitmaprenderer",
   "webgpu"
};


[
    ActiveDOMObject,
    GenerateIsReachable=Impl,
    EnabledBySetting=OffscreenCanvasEnabled,
    Conditional=OFFSCREEN_CANVAS,
    ConditionalForWorker=OFFSCREEN_CANVAS_IN_WORKERS,
    EnabledForContext,
    ExportMacro=WEBCORE_EXPORT,
    Exposed=(Window,Worker)
] interface OffscreenCanvas : EventTarget {
    [CallWith=CurrentScriptExecutionContext] constructor([EnforceRange] unsigned long width, [EnforceRange] unsigned long height);

    [CallTracer=InspectorCanvasCallTracer] attribute [EnforceRange] unsigned long width;
    [CallTracer=InspectorCanvasCallTracer] attribute [EnforceRange] unsigned long height;

    [CallWith=CurrentGlobalObject] OffscreenRenderingContext? getContext(OffscreenRenderingContextType contextType, any... arguments);
    
    ImageBitmap? transferToImageBitmap();
    Promise<Blob> convertToBlob(optional ImageEncodeOptions options = {});

    
    
    
    
};
```

### Modules/applepay/ApplePayErrorCode.idl

```webidl
[
    Conditional=APPLE_PAY,
    ExportMacro=WEBCORE_EXPORT,
] enum ApplePayErrorCode {
    "unknown",
    "shippingContactInvalid",
    "billingContactInvalid",
    "addressUnserviceable"
#if defined(ENABLE_APPLE_PAY_COUPON_CODE) && ENABLE_APPLE_PAY_COUPON_CODE
    , "couponCodeInvalid"
    , "couponCodeExpired"
#endif
#if defined(ENABLE_APPLE_PAY_DISBURSEMENTS) && ENABLE_APPLE_PAY_DISBURSEMENTS
    , "unsupportedCard"
    , "recipientContactInvalid"
#endif
};
```

### Modules/applepay/ApplePayFeature.idl

```webidl
[
    Conditional=APPLE_PAY,
] enum ApplePayFeature {
#if defined(ENABLE_APPLE_PAY_LATER) && ENABLE_APPLE_PAY_LATER
    "applePayLater",
#endif
#if defined(ENABLE_APPLE_PAY_LATER_AVAILABILITY) && ENABLE_APPLE_PAY_LATER_AVAILABILITY
    "applePayLaterAvailability",
#endif
#if defined(ENABLE_APPLE_PAY_PAYMENT_ORDER_DETAILS) && ENABLE_APPLE_PAY_PAYMENT_ORDER_DETAILS
    "authorizationResultOrderDetails",
#endif
    "lineItemPaymentTiming",
#if defined(ENABLE_APPLE_PAY_AUTOMATIC_RELOAD_PAYMENTS) && ENABLE_APPLE_PAY_AUTOMATIC_RELOAD_PAYMENTS
    "paymentRequestAutomaticReload",
#endif
#if defined(ENABLE_APPLE_PAY_COUPON_CODE) && ENABLE_APPLE_PAY_COUPON_CODE
    "paymentRequestCouponCode",
#endif
#if defined(ENABLE_APPLE_PAY_MERCHANT_CATEGORY_CODE) && ENABLE_APPLE_PAY_MERCHANT_CATEGORY_CODE
    "paymentRequestMerchantCategoryCode",
#endif
#if defined(ENABLE_APPLE_PAY_MULTI_MERCHANT_PAYMENTS) && ENABLE_APPLE_PAY_MULTI_MERCHANT_PAYMENTS
    "paymentRequestMultiTokenContexts",
#endif
#if defined(ENABLE_APPLE_PAY_RECURRING_PAYMENTS) && ENABLE_APPLE_PAY_RECURRING_PAYMENTS
    "paymentRequestRecurring",
#endif
#if defined(ENABLE_APPLE_PAY_SHIPPING_CONTACT_EDITING_MODE) && ENABLE_APPLE_PAY_SHIPPING_CONTACT_EDITING_MODE
    "paymentRequestShippingContactEditingMode",
#endif
#if defined(ENABLE_APPLE_PAY_AUTOMATIC_RELOAD_LINE_ITEM) && ENABLE_APPLE_PAY_AUTOMATIC_RELOAD_LINE_ITEM
    "paymentTimingAutomaticReload",
#endif
#if defined(ENABLE_APPLE_PAY_DEFERRED_PAYMENTS) && ENABLE_APPLE_PAY_DEFERRED_PAYMENTS
    "paymentRequestDeferred",
#endif
#if defined(ENABLE_APPLE_PAY_DEFERRED_LINE_ITEM) && ENABLE_APPLE_PAY_DEFERRED_LINE_ITEM
    "paymentTimingDeferred",
#endif
    "paymentTimingImmediate"
#if defined(ENABLE_APPLE_PAY_RECURRING_LINE_ITEM) && ENABLE_APPLE_PAY_RECURRING_LINE_ITEM
    , "paymentTimingRecurring"
#endif
#if defined(ENABLE_APPLE_PAY_SHIPPING_CONTACT_EDITING_MODE) && ENABLE_APPLE_PAY_SHIPPING_CONTACT_EDITING_MODE
    , "shippingContactEditingModeEnabled"
    , "shippingContactEditingModeStorePickup"
#endif
#if defined(ENABLE_APPLE_PAY_SHIPPING_METHOD_DATE_COMPONENTS_RANGE) && ENABLE_APPLE_PAY_SHIPPING_METHOD_DATE_COMPONENTS_RANGE
    , "shippingMethodDateComponentsRange"
#endif
#if defined(ENABLE_APPLE_PAY_DISBURSEMENTS) && ENABLE_APPLE_PAY_DISBURSEMENTS
    , "paymentRequestDisbursements"
#endif
#if defined(ENABLE_APPLE_PAY_DELEGATED_REQUEST) && ENABLE_APPLE_PAY_DELEGATED_REQUEST
    , "paymentRequestDelegation"
#endif
};
```

### Modules/applepay/ApplePayMerchantCapability.idl

```webidl
[
    Conditional=APPLE_PAY,
] enum ApplePayMerchantCapability {
    "supports3DS",
    "supportsEMV",
    "supportsCredit",
    "supportsDebit",
#if defined(ENABLE_APPLE_PAY_DISBURSEMENTS) && ENABLE_APPLE_PAY_DISBURSEMENTS
    "supportsInstantFundsOut"
#endif
};
```

### Modules/applepay/ApplePayPaymentTiming.idl

```webidl
enum ApplePayPaymentTiming {
    "immediate"
#if defined(ENABLE_APPLE_PAY_RECURRING_LINE_ITEM) && ENABLE_APPLE_PAY_RECURRING_LINE_ITEM
    , "recurring"
#endif
#if defined(ENABLE_APPLE_PAY_DEFERRED_LINE_ITEM) && ENABLE_APPLE_PAY_DEFERRED_LINE_ITEM
    , "deferred"
#endif
#if defined(ENABLE_APPLE_PAY_AUTOMATIC_RELOAD_LINE_ITEM) && ENABLE_APPLE_PAY_AUTOMATIC_RELOAD_LINE_ITEM
    , "automaticReload"
#endif
};
```

### Modules/mediasession/MediaSessionAction.idl

```webidl
[
    Conditional=MEDIA_SESSION,
] enum MediaSessionAction {
    "play",
    "pause",
    "seekbackward",
    "seekforward",
    "previoustrack",
    "nexttrack",
    "skipad",
    "stop",
    "seekto",
    "togglemicrophone",
    "togglecamera",
    "togglescreenshare",
    "hangup",
    "previousslide",
    "nextslide",
    "enterpictureinpicture",
    "voiceactivity",
    "togglecaptions",
    "selectcaptiontrack"
#if defined(ENABLE_MEDIA_SESSION_PLAYLIST) && ENABLE_MEDIA_SESSION_PLAYLIST
    , "settrack"
#endif
};
```

### Modules/ShapeDetection/BarcodeDetector.idl

```webidl
typedef (HTMLImageElement
#if defined(ENABLE_VIDEO) && ENABLE_VIDEO
    or HTMLVideoElement
#endif
    or HTMLCanvasElement
    or SVGImageElement
    or ImageBitmap
#if defined(ENABLE_OFFSCREEN_CANVAS) && ENABLE_OFFSCREEN_CANVAS
    or OffscreenCanvas
#endif
    or CSSStyleImageValue
#if defined(ENABLE_WEB_CODECS) && ENABLE_WEB_CODECS
    or WebCodecsVideoFrame
#endif
) CanvasImageSource;

typedef (CanvasImageSource or Blob or ImageData) ImageBitmapSource;

[
    EnabledBySetting=ShapeDetection,
    Exposed=(Window,Worker),
    SecureContext
]
interface BarcodeDetector {
    [CallWith=CurrentScriptExecutionContext] constructor(optional BarcodeDetectorOptions barcodeDetectorOptions = {});
    [CallWith=CurrentScriptExecutionContext] static Promise<sequence<BarcodeFormat>> getSupportedFormats();

    [CallWith=CurrentScriptExecutionContext] Promise<sequence<DetectedBarcode>> detect(ImageBitmapSource image);
};
```

### Modules/ShapeDetection/FaceDetector.idl

```webidl
typedef (HTMLImageElement
#if defined(ENABLE_VIDEO) && ENABLE_VIDEO
    or HTMLVideoElement
#endif
    or HTMLCanvasElement
    or SVGImageElement
    or ImageBitmap
#if defined(ENABLE_OFFSCREEN_CANVAS) && ENABLE_OFFSCREEN_CANVAS
    or OffscreenCanvas
#endif
    or CSSStyleImageValue
#if defined(ENABLE_WEB_CODECS) && ENABLE_WEB_CODECS
    or WebCodecsVideoFrame
#endif
) CanvasImageSource;

typedef (CanvasImageSource or Blob or ImageData) ImageBitmapSource;

[
    EnabledBySetting=ShapeDetection,
    Exposed=(Window,Worker),
    SecureContext
]
interface FaceDetector {
    [CallWith=CurrentScriptExecutionContext] constructor(optional FaceDetectorOptions faceDetectorOptions = {});
    [CallWith=CurrentScriptExecutionContext] Promise<sequence<DetectedFace>> detect(ImageBitmapSource image);
};
```

### Modules/ShapeDetection/TextDetector.idl

```webidl
typedef (HTMLImageElement
#if defined(ENABLE_VIDEO) && ENABLE_VIDEO
    or HTMLVideoElement
#endif
    or HTMLCanvasElement
    or SVGImageElement
    or ImageBitmap
#if defined(ENABLE_OFFSCREEN_CANVAS) && ENABLE_OFFSCREEN_CANVAS
    or OffscreenCanvas
#endif
    or CSSStyleImageValue
#if defined(ENABLE_WEB_CODECS) && ENABLE_WEB_CODECS
    or WebCodecsVideoFrame
#endif
) CanvasImageSource;

typedef (CanvasImageSource or Blob or ImageData) ImageBitmapSource;

[
    EnabledBySetting=ShapeDetection,
    Exposed=(Window,Worker),
    SecureContext
]
interface TextDetector {
    [CallWith=CurrentScriptExecutionContext] constructor();
    [CallWith=CurrentScriptExecutionContext] Promise<sequence<DetectedText>> detect(ImageBitmapSource image);
};
```

### Modules/webcodecs/WebCodecsVideoFrame.idl

```webidl
typedef (HTMLImageElement
    or SVGImageElement
    or HTMLCanvasElement
    or ImageBitmap
    or CSSStyleImageValue
#if defined(ENABLE_OFFSCREEN_CANVAS) && ENABLE_OFFSCREEN_CANVAS
    or OffscreenCanvas
#endif
#if defined(ENABLE_VIDEO) && ENABLE_VIDEO
    or HTMLVideoElement
#endif
) CanvasImageSource;


[
    ExportMacro=WEBCORE_EXPORT,
    Conditional=WEB_CODECS,
    EnabledBySetting=WebCodecsVideoEnabled,
    Exposed=(Window,DedicatedWorker),
    InterfaceName=VideoFrame,
    ReportExtraMemoryCost,
] interface WebCodecsVideoFrame {
    [CallWith=CurrentScriptExecutionContext] constructor(CanvasImageSource image, optional VideoFrameInit init = {});
    [CallWith=CurrentScriptExecutionContext] constructor(WebCodecsVideoFrame image, optional VideoFrameInit init = {});
    [CallWith=CurrentScriptExecutionContext] constructor([AllowShared] (ArrayBufferView or ArrayBuffer) data, VideoFrameBufferInit init);

    readonly attribute VideoPixelFormat? format;
    readonly attribute unsigned long codedWidth;
    readonly attribute unsigned long codedHeight;
    readonly attribute DOMRectReadOnly? codedRect;
    readonly attribute DOMRectReadOnly? visibleRect;
    readonly attribute unsigned long displayWidth;
    readonly attribute unsigned long displayHeight;
    readonly attribute unsigned long long? duration;
    readonly attribute long long timestamp;
    readonly attribute VideoColorSpace colorSpace;

    unsigned long allocationSize(optional VideoFrameCopyToOptions options = {});
    Promise<sequence<PlaneLayout>> copyTo([AllowShared] (ArrayBufferView or ArrayBuffer) destination, optional VideoFrameCopyToOptions options = {});
    [CallWith=CurrentScriptExecutionContext] WebCodecsVideoFrame clone();
    undefined close();
};

[
    Conditional=WEB_CODECS,
] dictionary VideoFrameInit {
    unsigned long long duration;
    long long timestamp;
    WebCodecsAlphaOption alpha = "keep";

    DOMRectInit visibleRect;

    [EnforceRange] unsigned long displayWidth;
    [EnforceRange] unsigned long displayHeight;
};

[
    Conditional=WEB_CODECS,
] dictionary VideoFrameBufferInit {
    required VideoPixelFormat format;
    required [EnforceRange] unsigned long codedWidth;
    required [EnforceRange] unsigned long codedHeight;
    required [EnforceRange] long long timestamp;
    [EnforceRange] unsigned long long duration;

    sequence<PlaneLayout> layout;

    DOMRectInit visibleRect;

    [EnforceRange] unsigned long displayWidth;
    [EnforceRange] unsigned long displayHeight;

    VideoColorSpaceInit colorSpace;
};

[
    Conditional=WEB_CODECS,
] dictionary VideoFrameCopyToOptions {
  DOMRectInit rect;
  sequence<PlaneLayout> layout;
};
```

### Modules/WebGPU/GPUExternalTextureDescriptor.idl

```webidl
[
    EnabledBySetting=WebGPUEnabled,
] dictionary GPUExternalTextureDescriptor : GPUObjectDescriptorBase {
#if defined(ENABLE_VIDEO) && ENABLE_VIDEO
#if defined(ENABLE_WEB_CODECS) && ENABLE_WEB_CODECS
    required (HTMLVideoElement or WebCodecsVideoFrame) source;
#else
    required HTMLVideoElement source;
#endif
#endif
    PredefinedColorSpace colorSpace = "srgb";
};
```

### Modules/WebGPU/GPUImageCopyExternalImage.idl

```webidl
typedef [EnforceRange] unsigned long GPUIntegerCoordinate;
typedef (sequence<GPUIntegerCoordinate> or GPUOrigin2DDict) GPUOrigin2D;



[
    EnabledBySetting=WebGPUEnabled,
] dictionary GPUImageCopyExternalImage {
    required (ImageBitmap or
#if defined(ENABLE_VIDEO) && ENABLE_VIDEO && defined(ENABLE_WEB_CODECS) && ENABLE_WEB_CODECS
        ImageData or HTMLImageElement or HTMLVideoElement or WebCodecsVideoFrame or
#endif
#if defined(ENABLE_OFFSCREEN_CANVAS) && ENABLE_OFFSCREEN_CANVAS
        OffscreenCanvas or
#endif
        HTMLCanvasElement
    ) source;
    GPUOrigin2D origin;
    boolean flipY = false;
};
```

### page/WindowOrWorkerGlobalScope.idl

```webidl
typedef (HTMLImageElement
#if defined(ENABLE_VIDEO) && ENABLE_VIDEO
    or HTMLVideoElement
#endif
    or HTMLCanvasElement
    or SVGImageElement
    or ImageBitmap
#if defined(ENABLE_OFFSCREEN_CANVAS) && ENABLE_OFFSCREEN_CANVAS
    or OffscreenCanvas
#endif
    or CSSStyleImageValue
#if defined(ENABLE_WEB_CODECS) && ENABLE_WEB_CODECS
    or WebCodecsVideoFrame
#endif
) CanvasImageSource;

typedef (CanvasImageSource or Blob or ImageData) ImageBitmapSource;


interface mixin WindowOrWorkerGlobalScope {
    [Replaceable] readonly attribute USVString origin;
    readonly attribute boolean isSecureContext;

    [EnabledBySetting=CrossOriginOpenerPolicyEnabled] readonly attribute boolean crossOriginIsolated;

    [CallWith=RelevantGlobalObject] undefined reportError(any error);

    
    DOMString atob(DOMString string);
    DOMString btoa(DOMString string);

    
    
    long setTimeout(ScheduledAction handler, optional long timeout = 0, any... arguments);
    undefined clearTimeout(optional long handle = 0);
    
    long setInterval(ScheduledAction handler, optional long timeout = 0, any... arguments);
    undefined clearInterval(optional long handle = 0);

    
    [Custom] undefined queueMicrotask(VoidCallback callback);

    
    Promise<ImageBitmap> createImageBitmap(ImageBitmapSource image, optional ImageBitmapOptions options = {});
    Promise<ImageBitmap> createImageBitmap(ImageBitmapSource image, long sx, long sy, long sw, long sh, optional ImageBitmapOptions options = {});

    
    [CallWith=CurrentGlobalObject&RelevantGlobalObject] any structuredClone(any value, optional StructuredSerializeOptions options = {});
};
```


## Existing legacy native exports

These additional hand-written operations must be checked before calling a generated omission a missing capability. The declaration list includes feature, host, rendering, input, geometry, observers, animation, clipboard/drag and accessibility operations.

```c
RV_EXPORT int32_t rv_abi_version(void);
RV_EXPORT int64_t rv_process_id(void);
RV_EXPORT RvHost* rv_host_create(int32_t width, int32_t height);
RV_EXPORT void rv_host_destroy(RvHost*);
RV_EXPORT int32_t rv_host_load_html(RvHost*, const char* html);
RV_EXPORT int32_t rv_host_scripts_enabled(RvHost*);
RV_EXPORT int32_t rv_host_vm_entry_disallowed(RvHost*);
RV_EXPORT RvNode* rv_document_body(RvHost*);
RV_EXPORT RvNode* rv_document_head(RvHost*);
RV_EXPORT RvNode* rv_document_get_element_by_id(RvHost*, const char* elementId);
RV_EXPORT RvNode* rv_document_create_element(RvHost*, const char* tagName);
RV_EXPORT RvNode* rv_document_create_text(RvHost*, const char* data);
RV_EXPORT int32_t rv_node_append_child(RvNode* parent, RvNode* child);
RV_EXPORT int32_t rv_node_set_text_content(RvNode*, const char* text);
RV_EXPORT int32_t rv_element_set_attribute(RvNode*, const char* name, const char* value);
RV_EXPORT void rv_node_release(RvNode*);
RV_EXPORT int32_t rv_host_paint_rgba(RvHost*, uint8_t* rgba, int32_t length);
RV_EXPORT int32_t rv_host_set_accelerated_compositing(RvHost*, int32_t mode);
RV_EXPORT int32_t rv_host_acquire_d3d11_frame(RvHost*, void* consumerDevice, RvGpuFrame** frame);
RV_EXPORT int32_t rv_gpu_frame_info(RvGpuFrame*, void** texture, int32_t* width, int32_t* height, int32_t* format);
RV_EXPORT int32_t rv_gpu_frame_release(RvGpuFrame*);
RV_EXPORT RvListener* rv_node_add_event_listener(RvNode* target, const char* type, int32_t capture, RvEventCallback callback, void* context);
RV_EXPORT int32_t rv_listener_remove(RvListener*);
RV_EXPORT int32_t rv_node_dispatch_event(RvNode* target, const char* type, int32_t bubbles, int32_t cancelable, int32_t* defaultPrevented);
RV_EXPORT int32_t rv_element_click(RvNode*);
RV_EXPORT int32_t rv_input_checked(RvNode*);
RV_EXPORT int32_t rv_event_phase(RvEvent*);
RV_EXPORT int32_t rv_event_default_prevented(RvEvent*);
RV_EXPORT void rv_event_prevent_default(RvEvent*);
RV_EXPORT void rv_event_stop_propagation(RvEvent*);
RV_EXPORT RvNode* rv_host_document(RvHost*);
RV_EXPORT RvNode* rv_document_create_comment(RvHost*, const char* data);
RV_EXPORT RvNode* rv_document_create_fragment(RvHost*);
RV_EXPORT RvNode* rv_document_import_node(RvHost*, RvNode*, int32_t deep);
RV_EXPORT RvNode* rv_node_parse_fragment(RvNode* context, const char* markup);
RV_EXPORT int32_t rv_node_type(RvNode*);
RV_EXPORT RvNode* rv_node_relative(RvNode*, int32_t relation);
RV_EXPORT int32_t rv_node_get_string(RvNode*, int32_t property, char* buffer, int32_t capacity, int32_t* length);
RV_EXPORT int32_t rv_node_set_string(RvNode*, int32_t property, const char* value);
RV_EXPORT int32_t rv_node_insert_before(RvNode* parent, RvNode* child, RvNode* reference);
RV_EXPORT int32_t rv_node_remove_child(RvNode* parent, RvNode* child);
RV_EXPORT int32_t rv_node_replace_child(RvNode* parent, RvNode* newChild, RvNode* oldChild);
RV_EXPORT int32_t rv_node_remove(RvNode*);
RV_EXPORT RvNode* rv_node_clone(RvNode*, int32_t deep);
RV_EXPORT int32_t rv_node_contains(RvNode* node, RvNode* other);
RV_EXPORT int32_t rv_node_is_connected(RvNode*);
RV_EXPORT int32_t rv_element_get_attribute(RvNode*, const char* name, char* buffer, int32_t capacity, int32_t* length);
RV_EXPORT int32_t rv_element_remove_attribute(RvNode*, const char* name);
RV_EXPORT int32_t rv_input_set_checked(RvNode*, int32_t checked);
RV_EXPORT RvEvent* rv_event_retain(RvEvent*);
RV_EXPORT void rv_event_release(RvEvent*);
RV_EXPORT RvEvent* rv_host_create_event(RvHost*, int32_t eventInterface, const char* type, int32_t bubbles, int32_t cancelable);
RV_EXPORT int32_t rv_node_dispatch(RvNode* target, RvEvent*, int32_t* defaultPrevented);
RV_EXPORT int32_t rv_event_interface(RvEvent*);
RV_EXPORT int32_t rv_event_flags(RvEvent*);
RV_EXPORT int32_t rv_event_type(RvEvent*, char* buffer, int32_t capacity, int32_t* length);
RV_EXPORT int32_t rv_event_key(RvEvent*, char* buffer, int32_t capacity, int32_t* length);
RV_EXPORT RvNode* rv_event_target(RvEvent*, int32_t current);
RV_EXPORT int32_t rv_last_exception(int32_t part, char* buffer, int32_t capacity, int32_t* length);
RV_EXPORT RvNode* rv_node_query_selector(RvNode* scope, const char* selectors, int32_t* status);
RV_EXPORT int32_t rv_node_query_selector_all(RvNode* scope, const char* selectors, RvNode** nodes, int32_t capacity, int32_t* count);
RV_EXPORT int32_t rv_node_query_selector_all_uncached(RvNode* scope, const char* selectors, RvNode** nodes, int32_t capacity, int32_t* count);
RV_EXPORT int32_t rv_node_ref_count(RvNode*);
RV_EXPORT RvWatch* rv_node_watch(RvNode*);
RV_EXPORT int32_t rv_watch_alive(RvWatch*);
RV_EXPORT RvNode* rv_watch_node(RvWatch*);
RV_EXPORT void rv_watch_release(RvWatch*);
RV_EXPORT void rv_release_memory(void);
RV_EXPORT void rv_collect_garbage(void);
RV_EXPORT void rv_collect_garbage_eden(void);
RV_EXPORT int32_t rv_node_has_wrapper(RvNode*);
RV_EXPORT int32_t rv_element_attribute_count(RvNode*);
RV_EXPORT int32_t rv_element_attribute_at(RvNode*, int32_t index, int32_t part, char* buffer, int32_t capacity, int32_t* length);
RV_EXPORT int32_t rv_element_focus(RvNode*);
RV_EXPORT RvNode* rv_document_active_element(RvHost*);
RV_EXPORT int32_t rv_run_loop_cycle(int32_t milliseconds);
RV_EXPORT int32_t rv_host_set_resource_handler(RvHost*, RvResourceHandler handler, void* context);
RV_EXPORT int32_t rv_resource_load_finish(RvResourceLoad*, const uint8_t* data, int32_t length, const char* contentType);
RV_EXPORT int32_t rv_resource_load_fail(RvResourceLoad*);
RV_EXPORT int32_t rv_abi_has(const char* feature);
RV_EXPORT int32_t rv_host_resize(RvHost*, int32_t width, int32_t height);
RV_EXPORT int32_t rv_host_set_device_scale(RvHost*, float scale);
RV_EXPORT int32_t rv_host_paint_size(RvHost*, int32_t* width, int32_t* height);
RV_EXPORT int32_t rv_host_mouse(RvHost*, int32_t action, int32_t x, int32_t y, int32_t button, int32_t clickCount, int32_t modifiers);
RV_EXPORT int32_t rv_host_wheel(RvHost*, int32_t x, int32_t y, float deltaX, float deltaY, int32_t modifiers);
RV_EXPORT int32_t rv_host_key(RvHost*, int32_t action, const char* key, const char* code, const char* text, int32_t modifiers, int32_t repeat);
RV_EXPORT int32_t rv_host_set_focused(RvHost*, int32_t focused);
RV_EXPORT int32_t rv_host_set_frame_callback(RvHost*, RvFrameCallback callback, void* context);
RV_EXPORT int32_t rv_host_update(RvHost*);
RV_EXPORT int32_t rv_host_dirty_rect(RvHost*, int32_t* x, int32_t* y, int32_t* width, int32_t* height);
RV_EXPORT int32_t rv_event_number(RvEvent*, int32_t number, double* value);
RV_EXPORT int32_t rv_event_string(RvEvent*, int32_t property, char* buffer, int32_t capacity, int32_t* length);
RV_EXPORT RvNode* rv_event_related_target(RvEvent*);
RV_EXPORT int32_t rv_host_cursor(RvHost*);
RV_EXPORT int32_t rv_host_scroll_position(RvHost*, int32_t* x, int32_t* y);
RV_EXPORT int32_t rv_host_scroll_to(RvHost*, int32_t x, int32_t y);
RV_EXPORT int32_t rv_host_ime_set_composition(RvHost*, const char* text, int32_t selectionStart, int32_t selectionEnd);
RV_EXPORT int32_t rv_host_ime_commit(RvHost*, const char* text);
RV_EXPORT int32_t rv_host_ime_cancel(RvHost*);
RV_EXPORT int32_t rv_host_ime_rect(RvHost*, int32_t* x, int32_t* y, int32_t* width, int32_t* height);
RV_EXPORT int32_t rv_element_class(RvNode*, const char* token, int32_t operation);
RV_EXPORT int32_t rv_element_set_style(RvNode*, const char* property, const char* value, int32_t important);
RV_EXPORT int32_t rv_element_get_style(RvNode*, const char* property, char* buffer, int32_t capacity, int32_t* length);
RV_EXPORT int32_t rv_element_computed_style(RvNode*, const char* property, char* buffer, int32_t capacity, int32_t* length);
RV_EXPORT int32_t rv_element_rect(RvNode*, double* x, double* y, double* width, double* height);
RV_EXPORT RvNode* rv_host_element_at(RvHost*, int32_t x, int32_t y);
RV_EXPORT int32_t rv_element_scroll_into_view(RvNode*, int32_t block, int32_t inlineAlignment, int32_t smooth);
RV_EXPORT int32_t rv_element_scroll_metric(RvNode*, int32_t metric, int32_t* value);
RV_EXPORT int32_t rv_element_scroll_to(RvNode*, int32_t left, int32_t top, int32_t smooth);
RV_EXPORT RvNode* rv_element_attach_shadow(RvNode*, int32_t* status);
RV_EXPORT RvNode* rv_element_shadow_root(RvNode*);
RV_EXPORT int32_t rv_text_selection(RvNode*, int32_t* start, int32_t* end);
RV_EXPORT int32_t rv_text_set_selection(RvNode*, int32_t start, int32_t end);
RV_EXPORT int32_t rv_element_animations(RvNode*, int32_t subtree, RvAnimation** animations, int32_t capacity, int32_t* count);
RV_EXPORT int32_t rv_host_animations(RvHost*, RvAnimation** animations, int32_t capacity, int32_t* count);
RV_EXPORT void rv_animation_release(RvAnimation*);
RV_EXPORT int32_t rv_animation_control(RvAnimation*, int32_t action);
RV_EXPORT int32_t rv_animation_state(RvAnimation*);
RV_EXPORT int32_t rv_animation_time(RvAnimation*, double* milliseconds);
RV_EXPORT int32_t rv_animation_seek(RvAnimation*, double milliseconds);
RV_EXPORT int32_t rv_animation_set_rate(RvAnimation*, double rate);
RV_EXPORT int32_t rv_animation_name(RvAnimation*, char* buffer, int32_t capacity, int32_t* length);
RV_EXPORT RvNode* rv_animation_target(RvAnimation*);
RV_EXPORT RvNode* rv_node_retain(RvNode*);
RV_EXPORT RvObserver* rv_host_observe_resize(RvHost*, RvResizeCallback callback, void* context);
RV_EXPORT RvObserver* rv_host_observe_intersection(RvHost*, RvIntersectionCallback callback, void* context, const char* rootMargin, const double* thresholds, int32_t thresholdCount);
RV_EXPORT int32_t rv_observer_observe(RvObserver*, RvNode* element);
RV_EXPORT int32_t rv_observer_unobserve(RvObserver*, RvNode* element);
RV_EXPORT void rv_observer_release(RvObserver*);
RV_EXPORT int32_t rv_element_snapshot(RvNode*, uint8_t* rgba, int32_t length, int32_t* width, int32_t* height);
RV_EXPORT int32_t rv_host_paint_rgba_rect(RvHost*, uint8_t* rgba, int32_t length, int32_t x, int32_t y, int32_t width, int32_t height);
RV_EXPORT int32_t rv_host_set_popup_handler(RvHost*, RvPopupCallback callback, void* context);
RV_EXPORT int32_t rv_popup_item_count(RvPopup*);
RV_EXPORT int32_t rv_popup_item(RvPopup*, int32_t index, int32_t* kind, int32_t* enabled, char* buffer, int32_t capacity, int32_t* length);
RV_EXPORT int32_t rv_popup_choose(RvPopup*, int32_t index);
RV_EXPORT int32_t rv_host_tooltip(RvHost*, char* buffer, int32_t capacity, int32_t* length);
RV_EXPORT int32_t rv_host_set_clipboard(RvHost*, int32_t style, RvClipboardWrite write, RvClipboardRead read, void* context);
RV_EXPORT int32_t rv_clipboard_offer(RvClipboardRequest*, const char* type, const char* data);
RV_EXPORT int32_t rv_host_edit(RvHost*, int32_t command);
RV_EXPORT int32_t rv_host_set_drag(RvHost*, int32_t style);
RV_EXPORT RvDragData* rv_drag_data_create(void);
RV_EXPORT int32_t rv_drag_data_add(RvDragData*, const char* type, const char* data);
RV_EXPORT void rv_drag_data_release(RvDragData*);
RV_EXPORT int32_t rv_host_drag(RvHost*, int32_t action, int32_t x, int32_t y, int32_t allowed, RvDragData* data, int32_t modifiers);
RV_EXPORT int32_t rv_host_set_drag_start(RvHost*, RvDragStartCallback callback, void* context);
RV_EXPORT int32_t rv_drag_item_count(RvDrag*);
RV_EXPORT int32_t rv_drag_item_type(RvDrag*, int32_t index, char* buffer, int32_t capacity, int32_t* length);
RV_EXPORT int32_t rv_drag_item_value(RvDrag*, int32_t index, char* buffer, int32_t capacity, int32_t* length);
RV_EXPORT RvDragData* rv_drag_data(RvDrag*);
RV_EXPORT int32_t rv_drag_image(RvDrag*, uint8_t* rgba, int32_t length, int32_t* width, int32_t* height, int32_t* offsetX, int32_t* offsetY);
RV_EXPORT int32_t rv_drag_end(RvDrag*, int32_t x, int32_t y, int32_t operation);
RV_EXPORT int32_t rv_host_set_accessibility(RvHost*, int32_t enabled, RvAxCallback callback, void* context);
RV_EXPORT int32_t rv_host_attach_uia(RvHost*, void* hwnd);
RV_EXPORT int32_t rv_host_detach_uia(RvHost*);
RV_EXPORT RvAxNode* rv_host_ax_root(RvHost*);
RV_EXPORT RvAxNode* rv_host_ax_focus(RvHost*);
RV_EXPORT RvAxNode* rv_host_ax_hit_test(RvHost*, int32_t x, int32_t y);
RV_EXPORT RvAxNode* rv_node_ax(RvNode*);
RV_EXPORT RvNode* rv_ax_dom_node(RvAxNode*);
RV_EXPORT RvAxNode* rv_ax_retain(RvAxNode*);
RV_EXPORT void rv_ax_release(RvAxNode*);
RV_EXPORT int64_t rv_ax_id(RvAxNode*);
RV_EXPORT int32_t rv_ax_role(RvAxNode*);
RV_EXPORT int32_t rv_ax_states(RvAxNode*);
RV_EXPORT int32_t rv_ax_string(RvAxNode*, int32_t property, char* buffer, int32_t capacity, int32_t* length);
RV_EXPORT int32_t rv_ax_number(RvAxNode*, int32_t property, double* value);
RV_EXPORT int32_t rv_ax_rect(RvAxNode*, double* x, double* y, double* width, double* height);
RV_EXPORT RvAxNode* rv_ax_parent(RvAxNode*);
RV_EXPORT int32_t rv_ax_children(RvAxNode*, RvAxNode** nodes, int32_t capacity, int32_t* count);
RV_EXPORT int32_t rv_ax_action(RvAxNode*, int32_t action);
RV_EXPORT int32_t rv_ax_set_value(RvAxNode*, const char* value);
RV_EXPORT int32_t rv_ax_set_number(RvAxNode*, double value);
RV_EXPORT int32_t rv_ax_text_length(RvAxNode*);
RV_EXPORT int32_t rv_ax_text(RvAxNode*, int32_t start, int32_t end, char* buffer, int32_t capacity, int32_t* length);
RV_EXPORT int32_t rv_ax_text_boundary(RvAxNode*, int32_t offset, int32_t unit, int32_t* start, int32_t* end);
RV_EXPORT int32_t rv_ax_text_selection(RvAxNode*, int32_t* start, int32_t* end);
RV_EXPORT int32_t rv_ax_text_caret(RvAxNode*);
RV_EXPORT int32_t rv_ax_text_set_selection(RvAxNode*, int32_t start, int32_t end);
RV_EXPORT int32_t rv_ax_text_rects(RvAxNode*, int32_t start, int32_t end, double* rects, int32_t capacity, int32_t* count);
RV_EXPORT int32_t rv_ax_text_offset_at(RvAxNode*, int32_t x, int32_t y);
RV_EXPORT int32_t rv_ax_text_child_range(RvAxNode* ancestor, RvAxNode* child, int32_t* start, int32_t* end);
```


## Existing generated native function coverage

The following is the complete 1,070-function generated baseline, grouped by interface. `get`, `set` and `op` distinguish exported accessors/operations; overloads retain their native function names. Inherited exports can appear under a concrete interface and legacy operations are additional.

### AbstractRange

- `startContainer` (getter): `rv_dom_abstract_range_get_start_container`.
- `startOffset` (getter): `rv_dom_abstract_range_get_start_offset`.
- `endContainer` (getter): `rv_dom_abstract_range_get_end_container`.
- `endOffset` (getter): `rv_dom_abstract_range_get_end_offset`.
- `collapsed` (getter): `rv_dom_abstract_range_get_collapsed`.

### Attr

- `namespaceURI` (getter): `rv_dom_attr_get_namespace_uri`.
- `prefix` (getter): `rv_dom_attr_get_prefix`.
- `localName` (getter): `rv_dom_attr_get_local_name`.
- `name` (getter): `rv_dom_attr_get_name`.
- `value` (getter): `rv_dom_attr_get_value`.
- `value` (setter): `rv_dom_attr_set_value`.
- `ownerElement` (getter): `rv_dom_attr_get_owner_element`.
- `specified` (getter): `rv_dom_attr_get_specified`.
- `style` (getter): `rv_dom_attr_get_style`.

### CSSStyleDeclaration

- `cssText` (getter): `rv_dom_css_style_declaration_get_css_text`.
- `cssText` (setter): `rv_dom_css_style_declaration_set_css_text`.
- `length` (getter): `rv_dom_css_style_declaration_get_length`.
- `item` (operation): `rv_dom_css_style_declaration_item`.
- `getPropertyValue` (operation): `rv_dom_css_style_declaration_get_property_value`.
- `getPropertyPriority` (operation): `rv_dom_css_style_declaration_get_property_priority`.
- `setProperty` (operation): `rv_dom_css_style_declaration_set_property`.
- `removeProperty` (operation): `rv_dom_css_style_declaration_remove_property`.

### CharacterData

- `data` (getter): `rv_dom_character_data_get_data`.
- `data` (setter): `rv_dom_character_data_set_data`.
- `length` (getter): `rv_dom_character_data_get_length`.
- `previousElementSibling` (getter): `rv_dom_character_data_get_previous_element_sibling`.
- `nextElementSibling` (getter): `rv_dom_character_data_get_next_element_sibling`.
- `substringData` (operation): `rv_dom_character_data_substring_data`.
- `appendData` (operation): `rv_dom_character_data_append_data`.
- `insertData` (operation): `rv_dom_character_data_insert_data`.
- `deleteData` (operation): `rv_dom_character_data_delete_data`.
- `replaceData` (operation): `rv_dom_character_data_replace_data`.
- `before` (operation): `rv_dom_character_data_before`.
- `after` (operation): `rv_dom_character_data_after`.
- `replaceWith` (operation): `rv_dom_character_data_replace_with`.
- `remove` (operation): `rv_dom_character_data_remove`.

### CompositionEvent

- `data` (getter): `rv_dom_composition_event_get_data`.

### DOMImplementation

- `createHTMLDocument` (operation): `rv_dom_dom_implementation_create_html_document`.
- `hasFeature` (operation): `rv_dom_dom_implementation_has_feature`.

### DOMRectReadOnly

- `x` (getter): `rv_dom_dom_rect_read_only_get_x`.
- `y` (getter): `rv_dom_dom_rect_read_only_get_y`.
- `width` (getter): `rv_dom_dom_rect_read_only_get_width`.
- `height` (getter): `rv_dom_dom_rect_read_only_get_height`.
- `top` (getter): `rv_dom_dom_rect_read_only_get_top`.
- `right` (getter): `rv_dom_dom_rect_read_only_get_right`.
- `bottom` (getter): `rv_dom_dom_rect_read_only_get_bottom`.
- `left` (getter): `rv_dom_dom_rect_read_only_get_left`.

### DOMSelection

- `anchorNode` (getter): `rv_dom_dom_selection_get_anchor_node`.
- `anchorOffset` (getter): `rv_dom_dom_selection_get_anchor_offset`.
- `focusNode` (getter): `rv_dom_dom_selection_get_focus_node`.
- `focusOffset` (getter): `rv_dom_dom_selection_get_focus_offset`.
- `isCollapsed` (getter): `rv_dom_dom_selection_get_is_collapsed`.
- `rangeCount` (getter): `rv_dom_dom_selection_get_range_count`.
- `type` (getter): `rv_dom_dom_selection_get_type`.
- `direction` (getter): `rv_dom_dom_selection_get_direction`.
- `baseNode` (getter): `rv_dom_dom_selection_get_base_node`.
- `baseOffset` (getter): `rv_dom_dom_selection_get_base_offset`.
- `extentNode` (getter): `rv_dom_dom_selection_get_extent_node`.
- `extentOffset` (getter): `rv_dom_dom_selection_get_extent_offset`.
- `getRangeAt` (operation): `rv_dom_dom_selection_get_range_at`.
- `addRange` (operation): `rv_dom_dom_selection_add_range`.
- `removeRange` (operation): `rv_dom_dom_selection_remove_range`.
- `removeAllRanges` (operation): `rv_dom_dom_selection_remove_all_ranges`.
- `getComposedRanges` (operation): `rv_dom_dom_selection_get_composed_ranges`.
- `empty` (operation): `rv_dom_dom_selection_empty`.
- `collapse` (operation): `rv_dom_dom_selection_collapse`.
- `setPosition` (operation): `rv_dom_dom_selection_set_position`.
- `collapseToStart` (operation): `rv_dom_dom_selection_collapse_to_start`.
- `collapseToEnd` (operation): `rv_dom_dom_selection_collapse_to_end`.
- `extend` (operation): `rv_dom_dom_selection_extend`.
- `setBaseAndExtent` (operation): `rv_dom_dom_selection_set_base_and_extent`.
- `selectAllChildren` (operation): `rv_dom_dom_selection_select_all_children`.
- `deleteFromDocument` (operation): `rv_dom_dom_selection_delete_from_document`.
- `containsNode` (operation): `rv_dom_dom_selection_contains_node`.
- `toString` (operation): `rv_dom_dom_selection_to_string`.
- `modify` (operation): `rv_dom_dom_selection_modify`.

### DOMStringMap

- `namedItem` (operation): `rv_dom_dom_string_map_named_item`.
- `setNamedItem` (operation): `rv_dom_dom_string_map_set_named_item`.
- `deleteNamedProperty` (operation): `rv_dom_dom_string_map_delete_named_property`.

### DOMTokenList

- `length` (getter): `rv_dom_dom_token_list_get_length`.
- `value` (getter): `rv_dom_dom_token_list_get_value`.
- `value` (setter): `rv_dom_dom_token_list_set_value`.
- `item` (operation): `rv_dom_dom_token_list_item`.
- `contains` (operation): `rv_dom_dom_token_list_contains`.
- `add` (operation): `rv_dom_dom_token_list_add`.
- `remove` (operation): `rv_dom_dom_token_list_remove`.
- `toggle` (operation): `rv_dom_dom_token_list_toggle`.
- `replace` (operation): `rv_dom_dom_token_list_replace`.
- `supports` (operation): `rv_dom_dom_token_list_supports`.

### Document

- `implementation` (getter): `rv_dom_document_get_implementation`.
- `compatMode` (getter): `rv_dom_document_get_compat_mode`.
- `characterSet` (getter): `rv_dom_document_get_character_set`.
- `contentType` (getter): `rv_dom_document_get_content_type`.
- `documentElement` (getter): `rv_dom_document_get_document_element`.
- `scrollingElement` (getter): `rv_dom_document_get_scrolling_element`.
- `readyState` (getter): `rv_dom_document_get_ready_state`.
- `title` (getter): `rv_dom_document_get_title`.
- `title` (setter): `rv_dom_document_set_title`.
- `dir` (getter): `rv_dom_document_get_dir`.
- `dir` (setter): `rv_dom_document_set_dir`.
- `body` (getter): `rv_dom_document_get_body`.
- `body` (setter): `rv_dom_document_set_body`.
- `head` (getter): `rv_dom_document_get_head`.
- `images` (getter): `rv_dom_document_get_images`.
- `links` (getter): `rv_dom_document_get_links`.
- `forms` (getter): `rv_dom_document_get_forms`.
- `scripts` (getter): `rv_dom_document_get_scripts`.
- `designMode` (getter): `rv_dom_document_get_design_mode`.
- `designMode` (setter): `rv_dom_document_set_design_mode`.
- `anchors` (getter): `rv_dom_document_get_anchors`.
- `hidden` (getter): `rv_dom_document_get_hidden`.
- `visibilityState` (getter): `rv_dom_document_get_visibility_state`.
- `children` (getter): `rv_dom_document_get_children`.
- `firstElementChild` (getter): `rv_dom_document_get_first_element_child`.
- `lastElementChild` (getter): `rv_dom_document_get_last_element_child`.
- `childElementCount` (getter): `rv_dom_document_get_child_element_count`.
- `activeElement` (getter): `rv_dom_document_get_active_element`.
- `createElement` (operation): `rv_dom_document_create_element`.
- `createElementNS` (operation): `rv_dom_document_create_element_ns`.
- `getElementsByTagName` (operation): `rv_dom_document_get_elements_by_tag_name`.
- `getElementsByTagNameNS` (operation): `rv_dom_document_get_elements_by_tag_name_ns`.
- `getElementsByClassName` (operation): `rv_dom_document_get_elements_by_class_name`.
- `createDocumentFragment` (operation): `rv_dom_document_create_document_fragment`.
- `createTextNode` (operation): `rv_dom_document_create_text_node`.
- `createComment` (operation): `rv_dom_document_create_comment`.
- `importNode` (operation): `rv_dom_document_import_node`.
- `adoptNode` (operation): `rv_dom_document_adopt_node`.
- `createAttribute` (operation): `rv_dom_document_create_attribute`.
- `createAttributeNS` (operation): `rv_dom_document_create_attribute_ns`.
- `createEvent` (operation): `rv_dom_document_create_event`.
- `createRange` (operation): `rv_dom_document_create_range`.
- `caretRangeFromPoint` (operation): `rv_dom_document_caret_range_from_point`.
- `ariaNotify` (operation): `rv_dom_document_aria_notify`.
- `ariaNotify` (operation): `rv_dom_document_aria_notify_2`.
- `getElementsByName` (operation): `rv_dom_document_get_elements_by_name`.
- `hasFocus` (operation): `rv_dom_document_has_focus`.
- `execCommand` (operation): `rv_dom_document_exec_command`.
- `queryCommandEnabled` (operation): `rv_dom_document_query_command_enabled`.
- `queryCommandIndeterm` (operation): `rv_dom_document_query_command_indeterm`.
- `queryCommandState` (operation): `rv_dom_document_query_command_state`.
- `queryCommandSupported` (operation): `rv_dom_document_query_command_supported`.
- `queryCommandValue` (operation): `rv_dom_document_query_command_value`.
- `getSelection` (operation): `rv_dom_document_get_selection`.
- `prepend` (operation): `rv_dom_document_prepend`.
- `append` (operation): `rv_dom_document_append`.
- `replaceChildren` (operation): `rv_dom_document_replace_children`.
- `moveBefore` (operation): `rv_dom_document_move_before`.
- `querySelector` (operation): `rv_dom_document_query_selector`.
- `querySelectorAll` (operation): `rv_dom_document_query_selector_all`.
- `getElementById` (operation): `rv_dom_document_get_element_by_id`.
- `elementFromPoint` (operation): `rv_dom_document_element_from_point`.
- `elementsFromPoint` (operation): `rv_dom_document_elements_from_point`.

### DocumentFragment

- `children` (getter): `rv_dom_document_fragment_get_children`.
- `firstElementChild` (getter): `rv_dom_document_fragment_get_first_element_child`.
- `lastElementChild` (getter): `rv_dom_document_fragment_get_last_element_child`.
- `childElementCount` (getter): `rv_dom_document_fragment_get_child_element_count`.
- `prepend` (operation): `rv_dom_document_fragment_prepend`.
- `append` (operation): `rv_dom_document_fragment_append`.
- `replaceChildren` (operation): `rv_dom_document_fragment_replace_children`.
- `moveBefore` (operation): `rv_dom_document_fragment_move_before`.
- `querySelector` (operation): `rv_dom_document_fragment_query_selector`.
- `querySelectorAll` (operation): `rv_dom_document_fragment_query_selector_all`.
- `getElementById` (operation): `rv_dom_document_fragment_get_element_by_id`.

### Element

- `namespaceURI` (getter): `rv_dom_element_get_namespace_uri`.
- `prefix` (getter): `rv_dom_element_get_prefix`.
- `localName` (getter): `rv_dom_element_get_local_name`.
- `tagName` (getter): `rv_dom_element_get_tag_name`.
- `id` (getter): `rv_dom_element_get_id`.
- `id` (setter): `rv_dom_element_set_id`.
- `className` (getter): `rv_dom_element_get_class_name`.
- `className` (setter): `rv_dom_element_set_class_name`.
- `classList` (getter): `rv_dom_element_get_class_list`.
- `slot` (getter): `rv_dom_element_get_slot`.
- `slot` (setter): `rv_dom_element_set_slot`.
- `part` (getter): `rv_dom_element_get_part`.
- `attributes` (getter): `rv_dom_element_get_attributes`.
- `scrollTop` (getter): `rv_dom_element_get_scroll_top`.
- `scrollTop` (setter): `rv_dom_element_set_scroll_top`.
- `scrollLeft` (getter): `rv_dom_element_get_scroll_left`.
- `scrollLeft` (setter): `rv_dom_element_set_scroll_left`.
- `scrollWidth` (getter): `rv_dom_element_get_scroll_width`.
- `scrollHeight` (getter): `rv_dom_element_get_scroll_height`.
- `clientTop` (getter): `rv_dom_element_get_client_top`.
- `clientLeft` (getter): `rv_dom_element_get_client_left`.
- `clientWidth` (getter): `rv_dom_element_get_client_width`.
- `clientHeight` (getter): `rv_dom_element_get_client_height`.
- `currentCSSZoom` (getter): `rv_dom_element_get_current_css_zoom`.
- `outerHTML` (getter): `rv_dom_element_get_outer_html`.
- `outerHTML` (setter): `rv_dom_element_set_outer_html`.
- `role` (getter): `rv_dom_element_get_role`.
- `role` (setter): `rv_dom_element_set_role`.
- `ariaActiveDescendantElement` (getter): `rv_dom_element_get_aria_active_descendant_element`.
- `ariaActiveDescendantElement` (setter): `rv_dom_element_set_aria_active_descendant_element`.
- `ariaAtomic` (getter): `rv_dom_element_get_aria_atomic`.
- `ariaAtomic` (setter): `rv_dom_element_set_aria_atomic`.
- `ariaAutoComplete` (getter): `rv_dom_element_get_aria_auto_complete`.
- `ariaAutoComplete` (setter): `rv_dom_element_set_aria_auto_complete`.
- `ariaBrailleLabel` (getter): `rv_dom_element_get_aria_braille_label`.
- `ariaBrailleLabel` (setter): `rv_dom_element_set_aria_braille_label`.
- `ariaBrailleRoleDescription` (getter): `rv_dom_element_get_aria_braille_role_description`.
- `ariaBrailleRoleDescription` (setter): `rv_dom_element_set_aria_braille_role_description`.
- `ariaBusy` (getter): `rv_dom_element_get_aria_busy`.
- `ariaBusy` (setter): `rv_dom_element_set_aria_busy`.
- `ariaChecked` (getter): `rv_dom_element_get_aria_checked`.
- `ariaChecked` (setter): `rv_dom_element_set_aria_checked`.
- `ariaColCount` (getter): `rv_dom_element_get_aria_col_count`.
- `ariaColCount` (setter): `rv_dom_element_set_aria_col_count`.
- `ariaColIndex` (getter): `rv_dom_element_get_aria_col_index`.
- `ariaColIndex` (setter): `rv_dom_element_set_aria_col_index`.
- `ariaColIndexText` (getter): `rv_dom_element_get_aria_col_index_text`.
- `ariaColIndexText` (setter): `rv_dom_element_set_aria_col_index_text`.
- `ariaColSpan` (getter): `rv_dom_element_get_aria_col_span`.
- `ariaColSpan` (setter): `rv_dom_element_set_aria_col_span`.
- `ariaCurrent` (getter): `rv_dom_element_get_aria_current`.
- `ariaCurrent` (setter): `rv_dom_element_set_aria_current`.
- `ariaDescription` (getter): `rv_dom_element_get_aria_description`.
- `ariaDescription` (setter): `rv_dom_element_set_aria_description`.
- `ariaDisabled` (getter): `rv_dom_element_get_aria_disabled`.
- `ariaDisabled` (setter): `rv_dom_element_set_aria_disabled`.
- `ariaExpanded` (getter): `rv_dom_element_get_aria_expanded`.
- `ariaExpanded` (setter): `rv_dom_element_set_aria_expanded`.
- `ariaHasPopup` (getter): `rv_dom_element_get_aria_has_popup`.
- `ariaHasPopup` (setter): `rv_dom_element_set_aria_has_popup`.
- `ariaHidden` (getter): `rv_dom_element_get_aria_hidden`.
- `ariaHidden` (setter): `rv_dom_element_set_aria_hidden`.
- `ariaInvalid` (getter): `rv_dom_element_get_aria_invalid`.
- `ariaInvalid` (setter): `rv_dom_element_set_aria_invalid`.
- `ariaKeyShortcuts` (getter): `rv_dom_element_get_aria_key_shortcuts`.
- `ariaKeyShortcuts` (setter): `rv_dom_element_set_aria_key_shortcuts`.
- `ariaLabel` (getter): `rv_dom_element_get_aria_label`.
- `ariaLabel` (setter): `rv_dom_element_set_aria_label`.
- `ariaLevel` (getter): `rv_dom_element_get_aria_level`.
- `ariaLevel` (setter): `rv_dom_element_set_aria_level`.
- `ariaLive` (getter): `rv_dom_element_get_aria_live`.
- `ariaLive` (setter): `rv_dom_element_set_aria_live`.
- `ariaModal` (getter): `rv_dom_element_get_aria_modal`.
- `ariaModal` (setter): `rv_dom_element_set_aria_modal`.
- `ariaMultiLine` (getter): `rv_dom_element_get_aria_multi_line`.
- `ariaMultiLine` (setter): `rv_dom_element_set_aria_multi_line`.
- `ariaMultiSelectable` (getter): `rv_dom_element_get_aria_multi_selectable`.
- `ariaMultiSelectable` (setter): `rv_dom_element_set_aria_multi_selectable`.
- `ariaOrientation` (getter): `rv_dom_element_get_aria_orientation`.
- `ariaOrientation` (setter): `rv_dom_element_set_aria_orientation`.
- `ariaPlaceholder` (getter): `rv_dom_element_get_aria_placeholder`.
- `ariaPlaceholder` (setter): `rv_dom_element_set_aria_placeholder`.
- `ariaPosInSet` (getter): `rv_dom_element_get_aria_pos_in_set`.
- `ariaPosInSet` (setter): `rv_dom_element_set_aria_pos_in_set`.
- `ariaPressed` (getter): `rv_dom_element_get_aria_pressed`.
- `ariaPressed` (setter): `rv_dom_element_set_aria_pressed`.
- `ariaReadOnly` (getter): `rv_dom_element_get_aria_read_only`.
- `ariaReadOnly` (setter): `rv_dom_element_set_aria_read_only`.
- `ariaRelevant` (getter): `rv_dom_element_get_aria_relevant`.
- `ariaRelevant` (setter): `rv_dom_element_set_aria_relevant`.
- `ariaRequired` (getter): `rv_dom_element_get_aria_required`.
- `ariaRequired` (setter): `rv_dom_element_set_aria_required`.
- `ariaRoleDescription` (getter): `rv_dom_element_get_aria_role_description`.
- `ariaRoleDescription` (setter): `rv_dom_element_set_aria_role_description`.
- `ariaRowCount` (getter): `rv_dom_element_get_aria_row_count`.
- `ariaRowCount` (setter): `rv_dom_element_set_aria_row_count`.
- `ariaRowIndex` (getter): `rv_dom_element_get_aria_row_index`.
- `ariaRowIndex` (setter): `rv_dom_element_set_aria_row_index`.
- `ariaRowIndexText` (getter): `rv_dom_element_get_aria_row_index_text`.
- `ariaRowIndexText` (setter): `rv_dom_element_set_aria_row_index_text`.
- `ariaRowSpan` (getter): `rv_dom_element_get_aria_row_span`.
- `ariaRowSpan` (setter): `rv_dom_element_set_aria_row_span`.
- `ariaSelected` (getter): `rv_dom_element_get_aria_selected`.
- `ariaSelected` (setter): `rv_dom_element_set_aria_selected`.
- `ariaSetSize` (getter): `rv_dom_element_get_aria_set_size`.
- `ariaSetSize` (setter): `rv_dom_element_set_aria_set_size`.
- `ariaSort` (getter): `rv_dom_element_get_aria_sort`.
- `ariaSort` (setter): `rv_dom_element_set_aria_sort`.
- `ariaValueMax` (getter): `rv_dom_element_get_aria_value_max`.
- `ariaValueMax` (setter): `rv_dom_element_set_aria_value_max`.
- `ariaValueMin` (getter): `rv_dom_element_get_aria_value_min`.
- `ariaValueMin` (setter): `rv_dom_element_set_aria_value_min`.
- `ariaValueNow` (getter): `rv_dom_element_get_aria_value_now`.
- `ariaValueNow` (setter): `rv_dom_element_set_aria_value_now`.
- `ariaValueText` (getter): `rv_dom_element_get_aria_value_text`.
- `ariaValueText` (setter): `rv_dom_element_set_aria_value_text`.
- `previousElementSibling` (getter): `rv_dom_element_get_previous_element_sibling`.
- `nextElementSibling` (getter): `rv_dom_element_get_next_element_sibling`.
- `children` (getter): `rv_dom_element_get_children`.
- `firstElementChild` (getter): `rv_dom_element_get_first_element_child`.
- `lastElementChild` (getter): `rv_dom_element_get_last_element_child`.
- `childElementCount` (getter): `rv_dom_element_get_child_element_count`.
- `assignedSlot` (getter): `rv_dom_element_get_assigned_slot`.
- `innerHTML` (getter): `rv_dom_element_get_inner_html`.
- `innerHTML` (setter): `rv_dom_element_set_inner_html`.
- `hasAttributes` (operation): `rv_dom_element_has_attributes`.
- `getAttributeNames` (operation): `rv_dom_element_get_attribute_names`.
- `getAttribute` (operation): `rv_dom_element_get_attribute`.
- `getAttributeNS` (operation): `rv_dom_element_get_attribute_ns`.
- `setAttribute` (operation): `rv_dom_element_set_attribute`.
- `setAttributeNS` (operation): `rv_dom_element_set_attribute_ns`.
- `removeAttribute` (operation): `rv_dom_element_remove_attribute`.
- `removeAttributeNS` (operation): `rv_dom_element_remove_attribute_ns`.
- `toggleAttribute` (operation): `rv_dom_element_toggle_attribute`.
- `hasAttribute` (operation): `rv_dom_element_has_attribute`.
- `hasAttributeNS` (operation): `rv_dom_element_has_attribute_ns`.
- `getAttributeNode` (operation): `rv_dom_element_get_attribute_node`.
- `getAttributeNodeNS` (operation): `rv_dom_element_get_attribute_node_ns`.
- `setAttributeNode` (operation): `rv_dom_element_set_attribute_node`.
- `setAttributeNodeNS` (operation): `rv_dom_element_set_attribute_node_ns`.
- `removeAttributeNode` (operation): `rv_dom_element_remove_attribute_node`.
- `attachShadow` (operation): `rv_dom_element_attach_shadow`.
- `closest` (operation): `rv_dom_element_closest`.
- `matches` (operation): `rv_dom_element_matches`.
- `getElementsByTagName` (operation): `rv_dom_element_get_elements_by_tag_name`.
- `getElementsByTagNameNS` (operation): `rv_dom_element_get_elements_by_tag_name_ns`.
- `getElementsByClassName` (operation): `rv_dom_element_get_elements_by_class_name`.
- `insertAdjacentElement` (operation): `rv_dom_element_insert_adjacent_element`.
- `insertAdjacentText` (operation): `rv_dom_element_insert_adjacent_text`.
- `ariaNotify` (operation): `rv_dom_element_aria_notify`.
- `ariaNotify` (operation): `rv_dom_element_aria_notify_2`.
- `getBoundingClientRect` (operation): `rv_dom_element_get_bounding_client_rect`.
- `checkVisibility` (operation): `rv_dom_element_check_visibility`.
- `scrollIntoView` (operation): `rv_dom_element_scroll_into_view`.
- `scroll` (operation): `rv_dom_element_scroll_1`.
- `scroll` (operation): `rv_dom_element_scroll`.
- `scrollTo` (operation): `rv_dom_element_scroll_to_1`.
- `scrollTo` (operation): `rv_dom_element_scroll_to`.
- `scrollBy` (operation): `rv_dom_element_scroll_by_1`.
- `scrollBy` (operation): `rv_dom_element_scroll_by`.
- `scrollIntoViewIfNeeded` (operation): `rv_dom_element_scroll_into_view_if_needed`.
- `insertAdjacentHTML` (operation): `rv_dom_element_insert_adjacent_html`.
- `setPointerCapture` (operation): `rv_dom_element_set_pointer_capture`.
- `releasePointerCapture` (operation): `rv_dom_element_release_pointer_capture`.
- `hasPointerCapture` (operation): `rv_dom_element_has_pointer_capture`.
- `before` (operation): `rv_dom_element_before`.
- `after` (operation): `rv_dom_element_after`.
- `replaceWith` (operation): `rv_dom_element_replace_with`.
- `remove` (operation): `rv_dom_element_remove`.
- `prepend` (operation): `rv_dom_element_prepend`.
- `append` (operation): `rv_dom_element_append`.
- `replaceChildren` (operation): `rv_dom_element_replace_children`.
- `moveBefore` (operation): `rv_dom_element_move_before`.
- `querySelector` (operation): `rv_dom_element_query_selector`.
- `querySelectorAll` (operation): `rv_dom_element_query_selector_all`.
- `setHTMLUnsafe` (operation): `rv_dom_element_set_html_unsafe`.
- `getHTML` (operation): `rv_dom_element_get_html`.

### Event

- `type` (getter): `rv_dom_event_get_type`.
- `target` (getter): `rv_dom_event_get_target`.
- `currentTarget` (getter): `rv_dom_event_get_current_target`.
- `eventPhase` (getter): `rv_dom_event_get_event_phase`.
- `cancelBubble` (getter): `rv_dom_event_get_cancel_bubble`.
- `cancelBubble` (setter): `rv_dom_event_set_cancel_bubble`.
- `bubbles` (getter): `rv_dom_event_get_bubbles`.
- `cancelable` (getter): `rv_dom_event_get_cancelable`.
- `defaultPrevented` (getter): `rv_dom_event_get_default_prevented`.
- `composed` (getter): `rv_dom_event_get_composed`.
- `isTrusted` (getter): `rv_dom_event_get_is_trusted`.
- `srcElement` (getter): `rv_dom_event_get_src_element`.
- `returnValue` (getter): `rv_dom_event_get_return_value`.
- `returnValue` (setter): `rv_dom_event_set_return_value`.
- `stopPropagation` (operation): `rv_dom_event_stop_propagation`.
- `stopImmediatePropagation` (operation): `rv_dom_event_stop_immediate_propagation`.
- `preventDefault` (operation): `rv_dom_event_prevent_default`.
- `initEvent` (operation): `rv_dom_event_init_event`.

### FocusEvent

- `relatedTarget` (getter): `rv_dom_focus_event_get_related_target`.

### HTMLAnchorElement

- `charset` (getter): `rv_dom_html_anchor_element_get_charset`.
- `charset` (setter): `rv_dom_html_anchor_element_set_charset`.
- `coords` (getter): `rv_dom_html_anchor_element_get_coords`.
- `coords` (setter): `rv_dom_html_anchor_element_set_coords`.
- `download` (getter): `rv_dom_html_anchor_element_get_download`.
- `download` (setter): `rv_dom_html_anchor_element_set_download`.
- `hreflang` (getter): `rv_dom_html_anchor_element_get_hreflang`.
- `hreflang` (setter): `rv_dom_html_anchor_element_set_hreflang`.
- `name` (getter): `rv_dom_html_anchor_element_get_name`.
- `name` (setter): `rv_dom_html_anchor_element_set_name`.
- `rel` (getter): `rv_dom_html_anchor_element_get_rel`.
- `rel` (setter): `rv_dom_html_anchor_element_set_rel`.
- `rev` (getter): `rv_dom_html_anchor_element_get_rev`.
- `rev` (setter): `rv_dom_html_anchor_element_set_rev`.
- `shape` (getter): `rv_dom_html_anchor_element_get_shape`.
- `shape` (setter): `rv_dom_html_anchor_element_set_shape`.
- `type` (getter): `rv_dom_html_anchor_element_get_type`.
- `type` (setter): `rv_dom_html_anchor_element_set_type`.
- `text` (getter): `rv_dom_html_anchor_element_get_text`.
- `text` (setter): `rv_dom_html_anchor_element_set_text`.
- `relList` (getter): `rv_dom_html_anchor_element_get_rel_list`.
- `referrerPolicy` (getter): `rv_dom_html_anchor_element_get_referrer_policy`.
- `referrerPolicy` (setter): `rv_dom_html_anchor_element_set_referrer_policy`.
- `origin` (getter): `rv_dom_html_anchor_element_get_origin`.
- `protocol` (getter): `rv_dom_html_anchor_element_get_protocol`.
- `protocol` (setter): `rv_dom_html_anchor_element_set_protocol`.
- `username` (getter): `rv_dom_html_anchor_element_get_username`.
- `username` (setter): `rv_dom_html_anchor_element_set_username`.
- `password` (getter): `rv_dom_html_anchor_element_get_password`.
- `password` (setter): `rv_dom_html_anchor_element_set_password`.
- `host` (getter): `rv_dom_html_anchor_element_get_host`.
- `host` (setter): `rv_dom_html_anchor_element_set_host`.
- `hostname` (getter): `rv_dom_html_anchor_element_get_hostname`.
- `hostname` (setter): `rv_dom_html_anchor_element_set_hostname`.
- `port` (getter): `rv_dom_html_anchor_element_get_port`.
- `port` (setter): `rv_dom_html_anchor_element_set_port`.
- `pathname` (getter): `rv_dom_html_anchor_element_get_pathname`.
- `pathname` (setter): `rv_dom_html_anchor_element_set_pathname`.
- `search` (getter): `rv_dom_html_anchor_element_get_search`.
- `search` (setter): `rv_dom_html_anchor_element_set_search`.
- `hash` (getter): `rv_dom_html_anchor_element_get_hash`.
- `hash` (setter): `rv_dom_html_anchor_element_set_hash`.
- `href` (getter): `rv_dom_html_anchor_element_get_href`.
- `href` (setter): `rv_dom_html_anchor_element_set_href`.
- `target` (getter): `rv_dom_html_anchor_element_get_target`.
- `target` (setter): `rv_dom_html_anchor_element_set_target`.

### HTMLBodyElement

- `aLink` (getter): `rv_dom_html_body_element_get_a_link`.
- `aLink` (setter): `rv_dom_html_body_element_set_a_link`.
- `background` (getter): `rv_dom_html_body_element_get_background`.
- `background` (setter): `rv_dom_html_body_element_set_background`.
- `bgColor` (getter): `rv_dom_html_body_element_get_bg_color`.
- `bgColor` (setter): `rv_dom_html_body_element_set_bg_color`.
- `link` (getter): `rv_dom_html_body_element_get_link`.
- `link` (setter): `rv_dom_html_body_element_set_link`.
- `text` (getter): `rv_dom_html_body_element_get_text`.
- `text` (setter): `rv_dom_html_body_element_set_text`.
- `vLink` (getter): `rv_dom_html_body_element_get_v_link`.
- `vLink` (setter): `rv_dom_html_body_element_set_v_link`.

### HTMLButtonElement

- `disabled` (getter): `rv_dom_html_button_element_get_disabled`.
- `disabled` (setter): `rv_dom_html_button_element_set_disabled`.
- `form` (getter): `rv_dom_html_button_element_get_form`.
- `formAction` (getter): `rv_dom_html_button_element_get_form_action`.
- `formAction` (setter): `rv_dom_html_button_element_set_form_action`.
- `formEnctype` (getter): `rv_dom_html_button_element_get_form_enctype`.
- `formEnctype` (setter): `rv_dom_html_button_element_set_form_enctype`.
- `formMethod` (getter): `rv_dom_html_button_element_get_form_method`.
- `formMethod` (setter): `rv_dom_html_button_element_set_form_method`.
- `type` (getter): `rv_dom_html_button_element_get_type`.
- `type` (setter): `rv_dom_html_button_element_set_type`.
- `formNoValidate` (getter): `rv_dom_html_button_element_get_form_no_validate`.
- `formNoValidate` (setter): `rv_dom_html_button_element_set_form_no_validate`.
- `formTarget` (getter): `rv_dom_html_button_element_get_form_target`.
- `formTarget` (setter): `rv_dom_html_button_element_set_form_target`.
- `name` (getter): `rv_dom_html_button_element_get_name`.
- `name` (setter): `rv_dom_html_button_element_set_name`.
- `value` (getter): `rv_dom_html_button_element_get_value`.
- `value` (setter): `rv_dom_html_button_element_set_value`.
- `commandForElement` (getter): `rv_dom_html_button_element_get_command_for_element`.
- `commandForElement` (setter): `rv_dom_html_button_element_set_command_for_element`.
- `command` (getter): `rv_dom_html_button_element_get_command`.
- `command` (setter): `rv_dom_html_button_element_set_command`.
- `willValidate` (getter): `rv_dom_html_button_element_get_will_validate`.
- `validity` (getter): `rv_dom_html_button_element_get_validity`.
- `validationMessage` (getter): `rv_dom_html_button_element_get_validation_message`.
- `labels` (getter): `rv_dom_html_button_element_get_labels`.
- `popoverTargetElement` (getter): `rv_dom_html_button_element_get_popover_target_element`.
- `popoverTargetElement` (setter): `rv_dom_html_button_element_set_popover_target_element`.
- `popoverTargetAction` (getter): `rv_dom_html_button_element_get_popover_target_action`.
- `popoverTargetAction` (setter): `rv_dom_html_button_element_set_popover_target_action`.
- `checkValidity` (operation): `rv_dom_html_button_element_check_validity`.
- `reportValidity` (operation): `rv_dom_html_button_element_report_validity`.
- `setCustomValidity` (operation): `rv_dom_html_button_element_set_custom_validity`.

### HTMLCollection

- `length` (getter): `rv_dom_html_collection_get_length`.
- `item` (operation): `rv_dom_html_collection_item`.
- `namedItem` (operation): `rv_dom_html_collection_named_item`.

### HTMLDetailsElement

- `name` (getter): `rv_dom_html_details_element_get_name`.
- `name` (setter): `rv_dom_html_details_element_set_name`.
- `open` (getter): `rv_dom_html_details_element_get_open`.
- `open` (setter): `rv_dom_html_details_element_set_open`.

### HTMLDialogElement

- `open` (getter): `rv_dom_html_dialog_element_get_open`.
- `open` (setter): `rv_dom_html_dialog_element_set_open`.
- `returnValue` (getter): `rv_dom_html_dialog_element_get_return_value`.
- `returnValue` (setter): `rv_dom_html_dialog_element_set_return_value`.
- `closedBy` (getter): `rv_dom_html_dialog_element_get_closed_by`.
- `closedBy` (setter): `rv_dom_html_dialog_element_set_closed_by`.
- `show` (operation): `rv_dom_html_dialog_element_show`.
- `showModal` (operation): `rv_dom_html_dialog_element_show_modal`.
- `close` (operation): `rv_dom_html_dialog_element_close`.
- `requestClose` (operation): `rv_dom_html_dialog_element_request_close`.

### HTMLDivElement

- `align` (getter): `rv_dom_html_div_element_get_align`.
- `align` (setter): `rv_dom_html_div_element_set_align`.

### HTMLElement

- `title` (getter): `rv_dom_html_element_get_title`.
- `title` (setter): `rv_dom_html_element_set_title`.
- `lang` (getter): `rv_dom_html_element_get_lang`.
- `lang` (setter): `rv_dom_html_element_set_lang`.
- `translate` (getter): `rv_dom_html_element_get_translate`.
- `translate` (setter): `rv_dom_html_element_set_translate`.
- `dir` (getter): `rv_dom_html_element_get_dir`.
- `dir` (setter): `rv_dom_html_element_set_dir`.
- `cue` (getter): `rv_dom_html_element_get_cue`.
- `cue` (setter): `rv_dom_html_element_set_cue`.
- `cuebackground` (getter): `rv_dom_html_element_get_cuebackground`.
- `cuebackground` (setter): `rv_dom_html_element_set_cuebackground`.
- `hidden` (getter): `rv_dom_html_element_get_hidden`.
- `hidden` (setter): `rv_dom_html_element_set_hidden`.
- `accessKey` (getter): `rv_dom_html_element_get_access_key`.
- `accessKey` (setter): `rv_dom_html_element_set_access_key`.
- `accessKeyLabel` (getter): `rv_dom_html_element_get_access_key_label`.
- `draggable` (getter): `rv_dom_html_element_get_draggable`.
- `draggable` (setter): `rv_dom_html_element_set_draggable`.
- `spellcheck` (getter): `rv_dom_html_element_get_spellcheck`.
- `spellcheck` (setter): `rv_dom_html_element_set_spellcheck`.
- `autocapitalize` (getter): `rv_dom_html_element_get_autocapitalize`.
- `autocapitalize` (setter): `rv_dom_html_element_set_autocapitalize`.
- `innerText` (getter): `rv_dom_html_element_get_inner_text`.
- `innerText` (setter): `rv_dom_html_element_set_inner_text`.
- `inert` (getter): `rv_dom_html_element_get_inert`.
- `inert` (setter): `rv_dom_html_element_set_inert`.
- `headingOffset` (getter): `rv_dom_html_element_get_heading_offset`.
- `headingOffset` (setter): `rv_dom_html_element_set_heading_offset`.
- `headingReset` (getter): `rv_dom_html_element_get_heading_reset`.
- `headingReset` (setter): `rv_dom_html_element_set_heading_reset`.
- `popover` (getter): `rv_dom_html_element_get_popover`.
- `popover` (setter): `rv_dom_html_element_set_popover`.
- `outerText` (getter): `rv_dom_html_element_get_outer_text`.
- `outerText` (setter): `rv_dom_html_element_set_outer_text`.
- `autocorrect` (getter): `rv_dom_html_element_get_autocorrect`.
- `autocorrect` (setter): `rv_dom_html_element_set_autocorrect`.
- `writingSuggestions` (getter): `rv_dom_html_element_get_writing_suggestions`.
- `writingSuggestions` (setter): `rv_dom_html_element_set_writing_suggestions`.
- `offsetParent` (getter): `rv_dom_html_element_get_offset_parent`.
- `offsetTop` (getter): `rv_dom_html_element_get_offset_top`.
- `offsetLeft` (getter): `rv_dom_html_element_get_offset_left`.
- `offsetWidth` (getter): `rv_dom_html_element_get_offset_width`.
- `offsetHeight` (getter): `rv_dom_html_element_get_offset_height`.
- `contentEditable` (getter): `rv_dom_html_element_get_content_editable`.
- `contentEditable` (setter): `rv_dom_html_element_set_content_editable`.
- `enterKeyHint` (getter): `rv_dom_html_element_get_enter_key_hint`.
- `enterKeyHint` (setter): `rv_dom_html_element_set_enter_key_hint`.
- `isContentEditable` (getter): `rv_dom_html_element_get_is_content_editable`.
- `inputMode` (getter): `rv_dom_html_element_get_input_mode`.
- `inputMode` (setter): `rv_dom_html_element_set_input_mode`.
- `dataset` (getter): `rv_dom_html_element_get_dataset`.
- `nonce` (getter): `rv_dom_html_element_get_nonce`.
- `nonce` (setter): `rv_dom_html_element_set_nonce`.
- `autofocus` (getter): `rv_dom_html_element_get_autofocus`.
- `autofocus` (setter): `rv_dom_html_element_set_autofocus`.
- `tabIndex` (getter): `rv_dom_html_element_get_tab_index`.
- `tabIndex` (setter): `rv_dom_html_element_set_tab_index`.
- `style` (getter): `rv_dom_html_element_get_style`.
- `click` (operation): `rv_dom_html_element_click`.
- `showPopover` (operation): `rv_dom_html_element_show_popover`.
- `hidePopover` (operation): `rv_dom_html_element_hide_popover`.
- `togglePopover` (operation): `rv_dom_html_element_toggle_popover`.
- `focus` (operation): `rv_dom_html_element_focus`.
- `blur` (operation): `rv_dom_html_element_blur`.

### HTMLFieldSetElement

- `disabled` (getter): `rv_dom_html_field_set_element_get_disabled`.
- `disabled` (setter): `rv_dom_html_field_set_element_set_disabled`.
- `form` (getter): `rv_dom_html_field_set_element_get_form`.
- `name` (getter): `rv_dom_html_field_set_element_get_name`.
- `name` (setter): `rv_dom_html_field_set_element_set_name`.
- `type` (getter): `rv_dom_html_field_set_element_get_type`.
- `elements` (getter): `rv_dom_html_field_set_element_get_elements`.
- `willValidate` (getter): `rv_dom_html_field_set_element_get_will_validate`.
- `validity` (getter): `rv_dom_html_field_set_element_get_validity`.
- `validationMessage` (getter): `rv_dom_html_field_set_element_get_validation_message`.
- `checkValidity` (operation): `rv_dom_html_field_set_element_check_validity`.
- `reportValidity` (operation): `rv_dom_html_field_set_element_report_validity`.
- `setCustomValidity` (operation): `rv_dom_html_field_set_element_set_custom_validity`.

### HTMLFormElement

- `acceptCharset` (getter): `rv_dom_html_form_element_get_accept_charset`.
- `acceptCharset` (setter): `rv_dom_html_form_element_set_accept_charset`.
- `action` (getter): `rv_dom_html_form_element_get_action`.
- `action` (setter): `rv_dom_html_form_element_set_action`.
- `autocomplete` (getter): `rv_dom_html_form_element_get_autocomplete`.
- `autocomplete` (setter): `rv_dom_html_form_element_set_autocomplete`.
- `enctype` (getter): `rv_dom_html_form_element_get_enctype`.
- `enctype` (setter): `rv_dom_html_form_element_set_enctype`.
- `encoding` (getter): `rv_dom_html_form_element_get_encoding`.
- `encoding` (setter): `rv_dom_html_form_element_set_encoding`.
- `method` (getter): `rv_dom_html_form_element_get_method`.
- `method` (setter): `rv_dom_html_form_element_set_method`.
- `name` (getter): `rv_dom_html_form_element_get_name`.
- `name` (setter): `rv_dom_html_form_element_set_name`.
- `noValidate` (getter): `rv_dom_html_form_element_get_no_validate`.
- `noValidate` (setter): `rv_dom_html_form_element_set_no_validate`.
- `target` (getter): `rv_dom_html_form_element_get_target`.
- `target` (setter): `rv_dom_html_form_element_set_target`.
- `rel` (getter): `rv_dom_html_form_element_get_rel`.
- `rel` (setter): `rv_dom_html_form_element_set_rel`.
- `relList` (getter): `rv_dom_html_form_element_get_rel_list`.
- `elements` (getter): `rv_dom_html_form_element_get_elements`.
- `length` (getter): `rv_dom_html_form_element_get_length`.
- `item` (operation): `rv_dom_html_form_element_item`.
- `namedItem` (operation): `rv_dom_html_form_element_named_item`.
- `reset` (operation): `rv_dom_html_form_element_reset`.
- `checkValidity` (operation): `rv_dom_html_form_element_check_validity`.
- `reportValidity` (operation): `rv_dom_html_form_element_report_validity`.

### HTMLImageElement

- `alt` (getter): `rv_dom_html_image_element_get_alt`.
- `alt` (setter): `rv_dom_html_image_element_set_alt`.
- `src` (getter): `rv_dom_html_image_element_get_src`.
- `src` (setter): `rv_dom_html_image_element_set_src`.
- `srcset` (getter): `rv_dom_html_image_element_get_srcset`.
- `srcset` (setter): `rv_dom_html_image_element_set_srcset`.
- `sizes` (getter): `rv_dom_html_image_element_get_sizes`.
- `sizes` (setter): `rv_dom_html_image_element_set_sizes`.
- `crossOrigin` (getter): `rv_dom_html_image_element_get_cross_origin`.
- `crossOrigin` (setter): `rv_dom_html_image_element_set_cross_origin`.
- `useMap` (getter): `rv_dom_html_image_element_get_use_map`.
- `useMap` (setter): `rv_dom_html_image_element_set_use_map`.
- `isMap` (getter): `rv_dom_html_image_element_get_is_map`.
- `isMap` (setter): `rv_dom_html_image_element_set_is_map`.
- `controls` (getter): `rv_dom_html_image_element_get_controls`.
- `controls` (setter): `rv_dom_html_image_element_set_controls`.
- `width` (getter): `rv_dom_html_image_element_get_width`.
- `width` (setter): `rv_dom_html_image_element_set_width`.
- `height` (getter): `rv_dom_html_image_element_get_height`.
- `height` (setter): `rv_dom_html_image_element_set_height`.
- `naturalHeight` (getter): `rv_dom_html_image_element_get_natural_height`.
- `naturalWidth` (getter): `rv_dom_html_image_element_get_natural_width`.
- `complete` (getter): `rv_dom_html_image_element_get_complete`.
- `currentSrc` (getter): `rv_dom_html_image_element_get_current_src`.
- `referrerPolicy` (getter): `rv_dom_html_image_element_get_referrer_policy`.
- `referrerPolicy` (setter): `rv_dom_html_image_element_set_referrer_policy`.
- `decoding` (getter): `rv_dom_html_image_element_get_decoding`.
- `decoding` (setter): `rv_dom_html_image_element_set_decoding`.
- `loading` (getter): `rv_dom_html_image_element_get_loading`.
- `loading` (setter): `rv_dom_html_image_element_set_loading`.
- `name` (getter): `rv_dom_html_image_element_get_name`.
- `name` (setter): `rv_dom_html_image_element_set_name`.
- `lowsrc` (getter): `rv_dom_html_image_element_get_lowsrc`.
- `lowsrc` (setter): `rv_dom_html_image_element_set_lowsrc`.
- `align` (getter): `rv_dom_html_image_element_get_align`.
- `align` (setter): `rv_dom_html_image_element_set_align`.
- `hspace` (getter): `rv_dom_html_image_element_get_hspace`.
- `hspace` (setter): `rv_dom_html_image_element_set_hspace`.
- `vspace` (getter): `rv_dom_html_image_element_get_vspace`.
- `vspace` (setter): `rv_dom_html_image_element_set_vspace`.
- `longDesc` (getter): `rv_dom_html_image_element_get_long_desc`.
- `longDesc` (setter): `rv_dom_html_image_element_set_long_desc`.
- `border` (getter): `rv_dom_html_image_element_get_border`.
- `border` (setter): `rv_dom_html_image_element_set_border`.
- `fetchPriority` (getter): `rv_dom_html_image_element_get_fetch_priority`.
- `fetchPriority` (setter): `rv_dom_html_image_element_set_fetch_priority`.
- `attachmentIdentifier` (getter): `rv_dom_html_image_element_get_attachment_identifier`.
- `x` (getter): `rv_dom_html_image_element_get_x`.
- `y` (getter): `rv_dom_html_image_element_get_y`.

### HTMLInputElement

- `accept` (getter): `rv_dom_html_input_element_get_accept`.
- `accept` (setter): `rv_dom_html_input_element_set_accept`.
- `alpha` (getter): `rv_dom_html_input_element_get_alpha`.
- `alpha` (setter): `rv_dom_html_input_element_set_alpha`.
- `alt` (getter): `rv_dom_html_input_element_get_alt`.
- `alt` (setter): `rv_dom_html_input_element_set_alt`.
- `autocomplete` (getter): `rv_dom_html_input_element_get_autocomplete`.
- `autocomplete` (setter): `rv_dom_html_input_element_set_autocomplete`.
- `defaultChecked` (getter): `rv_dom_html_input_element_get_default_checked`.
- `defaultChecked` (setter): `rv_dom_html_input_element_set_default_checked`.
- `checked` (getter): `rv_dom_html_input_element_get_checked`.
- `checked` (setter): `rv_dom_html_input_element_set_checked`.
- `colorSpace` (getter): `rv_dom_html_input_element_get_color_space`.
- `colorSpace` (setter): `rv_dom_html_input_element_set_color_space`.
- `dirName` (getter): `rv_dom_html_input_element_get_dir_name`.
- `dirName` (setter): `rv_dom_html_input_element_set_dir_name`.
- `disabled` (getter): `rv_dom_html_input_element_get_disabled`.
- `disabled` (setter): `rv_dom_html_input_element_set_disabled`.
- `form` (getter): `rv_dom_html_input_element_get_form`.
- `formAction` (getter): `rv_dom_html_input_element_get_form_action`.
- `formAction` (setter): `rv_dom_html_input_element_set_form_action`.
- `formEnctype` (getter): `rv_dom_html_input_element_get_form_enctype`.
- `formEnctype` (setter): `rv_dom_html_input_element_set_form_enctype`.
- `formMethod` (getter): `rv_dom_html_input_element_get_form_method`.
- `formMethod` (setter): `rv_dom_html_input_element_set_form_method`.
- `formNoValidate` (getter): `rv_dom_html_input_element_get_form_no_validate`.
- `formNoValidate` (setter): `rv_dom_html_input_element_set_form_no_validate`.
- `formTarget` (getter): `rv_dom_html_input_element_get_form_target`.
- `formTarget` (setter): `rv_dom_html_input_element_set_form_target`.
- `height` (getter): `rv_dom_html_input_element_get_height`.
- `height` (setter): `rv_dom_html_input_element_set_height`.
- `indeterminate` (getter): `rv_dom_html_input_element_get_indeterminate`.
- `indeterminate` (setter): `rv_dom_html_input_element_set_indeterminate`.
- `list` (getter): `rv_dom_html_input_element_get_list`.
- `max` (getter): `rv_dom_html_input_element_get_max`.
- `max` (setter): `rv_dom_html_input_element_set_max`.
- `minLength` (getter): `rv_dom_html_input_element_get_min_length`.
- `minLength` (setter): `rv_dom_html_input_element_set_min_length`.
- `maxLength` (getter): `rv_dom_html_input_element_get_max_length`.
- `maxLength` (setter): `rv_dom_html_input_element_set_max_length`.
- `min` (getter): `rv_dom_html_input_element_get_min`.
- `min` (setter): `rv_dom_html_input_element_set_min`.
- `multiple` (getter): `rv_dom_html_input_element_get_multiple`.
- `multiple` (setter): `rv_dom_html_input_element_set_multiple`.
- `name` (getter): `rv_dom_html_input_element_get_name`.
- `name` (setter): `rv_dom_html_input_element_set_name`.
- `pattern` (getter): `rv_dom_html_input_element_get_pattern`.
- `pattern` (setter): `rv_dom_html_input_element_set_pattern`.
- `placeholder` (getter): `rv_dom_html_input_element_get_placeholder`.
- `placeholder` (setter): `rv_dom_html_input_element_set_placeholder`.
- `readOnly` (getter): `rv_dom_html_input_element_get_read_only`.
- `readOnly` (setter): `rv_dom_html_input_element_set_read_only`.
- `required` (getter): `rv_dom_html_input_element_get_required`.
- `required` (setter): `rv_dom_html_input_element_set_required`.
- `size` (getter): `rv_dom_html_input_element_get_size`.
- `size` (setter): `rv_dom_html_input_element_set_size`.
- `src` (getter): `rv_dom_html_input_element_get_src`.
- `src` (setter): `rv_dom_html_input_element_set_src`.
- `step` (getter): `rv_dom_html_input_element_get_step`.
- `step` (setter): `rv_dom_html_input_element_set_step`.
- `switch` (getter): `rv_dom_html_input_element_get_switch`.
- `switch` (setter): `rv_dom_html_input_element_set_switch`.
- `type` (getter): `rv_dom_html_input_element_get_type`.
- `type` (setter): `rv_dom_html_input_element_set_type`.
- `defaultValue` (getter): `rv_dom_html_input_element_get_default_value`.
- `defaultValue` (setter): `rv_dom_html_input_element_set_default_value`.
- `value` (getter): `rv_dom_html_input_element_get_value`.
- `value` (setter): `rv_dom_html_input_element_set_value`.
- `valueAsNumber` (getter): `rv_dom_html_input_element_get_value_as_number`.
- `valueAsNumber` (setter): `rv_dom_html_input_element_set_value_as_number`.
- `width` (getter): `rv_dom_html_input_element_get_width`.
- `width` (setter): `rv_dom_html_input_element_set_width`.
- `willValidate` (getter): `rv_dom_html_input_element_get_will_validate`.
- `validity` (getter): `rv_dom_html_input_element_get_validity`.
- `validationMessage` (getter): `rv_dom_html_input_element_get_validation_message`.
- `labels` (getter): `rv_dom_html_input_element_get_labels`.
- `selectionStart` (getter): `rv_dom_html_input_element_get_selection_start`.
- `selectionStart` (setter): `rv_dom_html_input_element_set_selection_start`.
- `selectionEnd` (getter): `rv_dom_html_input_element_get_selection_end`.
- `selectionEnd` (setter): `rv_dom_html_input_element_set_selection_end`.
- `selectionDirection` (getter): `rv_dom_html_input_element_get_selection_direction`.
- `selectionDirection` (setter): `rv_dom_html_input_element_set_selection_direction`.
- `align` (getter): `rv_dom_html_input_element_get_align`.
- `align` (setter): `rv_dom_html_input_element_set_align`.
- `useMap` (getter): `rv_dom_html_input_element_get_use_map`.
- `useMap` (setter): `rv_dom_html_input_element_set_use_map`.
- `capture` (getter): `rv_dom_html_input_element_get_capture`.
- `capture` (setter): `rv_dom_html_input_element_set_capture`.
- `popoverTargetElement` (getter): `rv_dom_html_input_element_get_popover_target_element`.
- `popoverTargetElement` (setter): `rv_dom_html_input_element_set_popover_target_element`.
- `popoverTargetAction` (getter): `rv_dom_html_input_element_get_popover_target_action`.
- `popoverTargetAction` (setter): `rv_dom_html_input_element_set_popover_target_action`.
- `stepUp` (operation): `rv_dom_html_input_element_step_up`.
- `stepDown` (operation): `rv_dom_html_input_element_step_down`.
- `checkValidity` (operation): `rv_dom_html_input_element_check_validity`.
- `reportValidity` (operation): `rv_dom_html_input_element_report_validity`.
- `setCustomValidity` (operation): `rv_dom_html_input_element_set_custom_validity`.
- `select` (operation): `rv_dom_html_input_element_select`.
- `setRangeText` (operation): `rv_dom_html_input_element_set_range_text_1`.
- `setRangeText` (operation): `rv_dom_html_input_element_set_range_text_4`.
- `setSelectionRange` (operation): `rv_dom_html_input_element_set_selection_range`.

### HTMLLIElement

- `type` (getter): `rv_dom_htmlli_element_get_type`.
- `type` (setter): `rv_dom_htmlli_element_set_type`.
- `value` (getter): `rv_dom_htmlli_element_get_value`.
- `value` (setter): `rv_dom_htmlli_element_set_value`.

### HTMLLabelElement

- `form` (getter): `rv_dom_html_label_element_get_form`.
- `htmlFor` (getter): `rv_dom_html_label_element_get_html_for`.
- `htmlFor` (setter): `rv_dom_html_label_element_set_html_for`.
- `control` (getter): `rv_dom_html_label_element_get_control`.

### HTMLLegendElement

- `form` (getter): `rv_dom_html_legend_element_get_form`.
- `align` (getter): `rv_dom_html_legend_element_get_align`.
- `align` (setter): `rv_dom_html_legend_element_set_align`.

### HTMLMeterElement

- `value` (getter): `rv_dom_html_meter_element_get_value`.
- `value` (setter): `rv_dom_html_meter_element_set_value`.
- `min` (getter): `rv_dom_html_meter_element_get_min`.
- `min` (setter): `rv_dom_html_meter_element_set_min`.
- `max` (getter): `rv_dom_html_meter_element_get_max`.
- `max` (setter): `rv_dom_html_meter_element_set_max`.
- `low` (getter): `rv_dom_html_meter_element_get_low`.
- `low` (setter): `rv_dom_html_meter_element_set_low`.
- `high` (getter): `rv_dom_html_meter_element_get_high`.
- `high` (setter): `rv_dom_html_meter_element_set_high`.
- `optimum` (getter): `rv_dom_html_meter_element_get_optimum`.
- `optimum` (setter): `rv_dom_html_meter_element_set_optimum`.
- `labels` (getter): `rv_dom_html_meter_element_get_labels`.

### HTMLOListElement

- `compact` (getter): `rv_dom_htmlo_list_element_get_compact`.
- `compact` (setter): `rv_dom_htmlo_list_element_set_compact`.
- `start` (getter): `rv_dom_htmlo_list_element_get_start`.
- `start` (setter): `rv_dom_htmlo_list_element_set_start`.
- `reversed` (getter): `rv_dom_htmlo_list_element_get_reversed`.
- `reversed` (setter): `rv_dom_htmlo_list_element_set_reversed`.
- `type` (getter): `rv_dom_htmlo_list_element_get_type`.
- `type` (setter): `rv_dom_htmlo_list_element_set_type`.

### HTMLOptGroupElement

- `disabled` (getter): `rv_dom_html_opt_group_element_get_disabled`.
- `disabled` (setter): `rv_dom_html_opt_group_element_set_disabled`.
- `label` (getter): `rv_dom_html_opt_group_element_get_label`.
- `label` (setter): `rv_dom_html_opt_group_element_set_label`.

### HTMLOptionElement

- `disabled` (getter): `rv_dom_html_option_element_get_disabled`.
- `disabled` (setter): `rv_dom_html_option_element_set_disabled`.
- `form` (getter): `rv_dom_html_option_element_get_form`.
- `label` (getter): `rv_dom_html_option_element_get_label`.
- `label` (setter): `rv_dom_html_option_element_set_label`.
- `defaultSelected` (getter): `rv_dom_html_option_element_get_default_selected`.
- `defaultSelected` (setter): `rv_dom_html_option_element_set_default_selected`.
- `selected` (getter): `rv_dom_html_option_element_get_selected`.
- `selected` (setter): `rv_dom_html_option_element_set_selected`.
- `value` (getter): `rv_dom_html_option_element_get_value`.
- `value` (setter): `rv_dom_html_option_element_set_value`.
- `text` (getter): `rv_dom_html_option_element_get_text`.
- `text` (setter): `rv_dom_html_option_element_set_text`.
- `index` (getter): `rv_dom_html_option_element_get_index`.

### HTMLOutputElement

- `htmlFor` (getter): `rv_dom_html_output_element_get_html_for`.
- `form` (getter): `rv_dom_html_output_element_get_form`.
- `name` (getter): `rv_dom_html_output_element_get_name`.
- `name` (setter): `rv_dom_html_output_element_set_name`.
- `type` (getter): `rv_dom_html_output_element_get_type`.
- `defaultValue` (getter): `rv_dom_html_output_element_get_default_value`.
- `defaultValue` (setter): `rv_dom_html_output_element_set_default_value`.
- `value` (getter): `rv_dom_html_output_element_get_value`.
- `value` (setter): `rv_dom_html_output_element_set_value`.
- `willValidate` (getter): `rv_dom_html_output_element_get_will_validate`.
- `validity` (getter): `rv_dom_html_output_element_get_validity`.
- `validationMessage` (getter): `rv_dom_html_output_element_get_validation_message`.
- `labels` (getter): `rv_dom_html_output_element_get_labels`.
- `checkValidity` (operation): `rv_dom_html_output_element_check_validity`.
- `reportValidity` (operation): `rv_dom_html_output_element_report_validity`.
- `setCustomValidity` (operation): `rv_dom_html_output_element_set_custom_validity`.

### HTMLParagraphElement

- `align` (getter): `rv_dom_html_paragraph_element_get_align`.
- `align` (setter): `rv_dom_html_paragraph_element_set_align`.

### HTMLProgressElement

- `value` (getter): `rv_dom_html_progress_element_get_value`.
- `value` (setter): `rv_dom_html_progress_element_set_value`.
- `max` (getter): `rv_dom_html_progress_element_get_max`.
- `max` (setter): `rv_dom_html_progress_element_set_max`.
- `position` (getter): `rv_dom_html_progress_element_get_position`.
- `labels` (getter): `rv_dom_html_progress_element_get_labels`.

### HTMLSelectElement

- `autocomplete` (getter): `rv_dom_html_select_element_get_autocomplete`.
- `autocomplete` (setter): `rv_dom_html_select_element_set_autocomplete`.
- `disabled` (getter): `rv_dom_html_select_element_get_disabled`.
- `disabled` (setter): `rv_dom_html_select_element_set_disabled`.
- `form` (getter): `rv_dom_html_select_element_get_form`.
- `multiple` (getter): `rv_dom_html_select_element_get_multiple`.
- `multiple` (setter): `rv_dom_html_select_element_set_multiple`.
- `name` (getter): `rv_dom_html_select_element_get_name`.
- `name` (setter): `rv_dom_html_select_element_set_name`.
- `required` (getter): `rv_dom_html_select_element_get_required`.
- `required` (setter): `rv_dom_html_select_element_set_required`.
- `size` (getter): `rv_dom_html_select_element_get_size`.
- `size` (setter): `rv_dom_html_select_element_set_size`.
- `type` (getter): `rv_dom_html_select_element_get_type`.
- `options` (getter): `rv_dom_html_select_element_get_options`.
- `length` (getter): `rv_dom_html_select_element_get_length`.
- `length` (setter): `rv_dom_html_select_element_set_length`.
- `selectedOptions` (getter): `rv_dom_html_select_element_get_selected_options`.
- `selectedIndex` (getter): `rv_dom_html_select_element_get_selected_index`.
- `selectedIndex` (setter): `rv_dom_html_select_element_set_selected_index`.
- `value` (getter): `rv_dom_html_select_element_get_value`.
- `value` (setter): `rv_dom_html_select_element_set_value`.
- `willValidate` (getter): `rv_dom_html_select_element_get_will_validate`.
- `validity` (getter): `rv_dom_html_select_element_get_validity`.
- `validationMessage` (getter): `rv_dom_html_select_element_get_validation_message`.
- `labels` (getter): `rv_dom_html_select_element_get_labels`.
- `item` (operation): `rv_dom_html_select_element_item`.
- `namedItem` (operation): `rv_dom_html_select_element_named_item`.
- `add` (operation): `rv_dom_html_select_element_add`.
- `remove` (operation): `rv_dom_html_select_element_remove_0`.
- `remove` (operation): `rv_dom_html_select_element_remove_1`.
- `setItem` (operation): `rv_dom_html_select_element_set_item`.
- `checkValidity` (operation): `rv_dom_html_select_element_check_validity`.
- `reportValidity` (operation): `rv_dom_html_select_element_report_validity`.
- `setCustomValidity` (operation): `rv_dom_html_select_element_set_custom_validity`.

### HTMLSlotElement

- `name` (getter): `rv_dom_html_slot_element_get_name`.
- `name` (setter): `rv_dom_html_slot_element_set_name`.
- `assignedNodes` (operation): `rv_dom_html_slot_element_assigned_nodes`.
- `assignedElements` (operation): `rv_dom_html_slot_element_assigned_elements`.
- `assign` (operation): `rv_dom_html_slot_element_assign`.

### HTMLTemplateElement

- `content` (getter): `rv_dom_html_template_element_get_content`.
- `shadowRootMode` (getter): `rv_dom_html_template_element_get_shadow_root_mode`.
- `shadowRootMode` (setter): `rv_dom_html_template_element_set_shadow_root_mode`.
- `shadowRootDelegatesFocus` (getter): `rv_dom_html_template_element_get_shadow_root_delegates_focus`.
- `shadowRootDelegatesFocus` (setter): `rv_dom_html_template_element_set_shadow_root_delegates_focus`.
- `shadowRootSlotAssignment` (getter): `rv_dom_html_template_element_get_shadow_root_slot_assignment`.
- `shadowRootSlotAssignment` (setter): `rv_dom_html_template_element_set_shadow_root_slot_assignment`.
- `shadowRootClonable` (getter): `rv_dom_html_template_element_get_shadow_root_clonable`.
- `shadowRootClonable` (setter): `rv_dom_html_template_element_set_shadow_root_clonable`.
- `shadowRootSerializable` (getter): `rv_dom_html_template_element_get_shadow_root_serializable`.
- `shadowRootSerializable` (setter): `rv_dom_html_template_element_set_shadow_root_serializable`.
- `shadowRootCustomElementRegistry` (getter): `rv_dom_html_template_element_get_shadow_root_custom_element_registry`.
- `shadowRootCustomElementRegistry` (setter): `rv_dom_html_template_element_set_shadow_root_custom_element_registry`.
- `shadowRootReferenceTarget` (getter): `rv_dom_html_template_element_get_shadow_root_reference_target`.
- `shadowRootReferenceTarget` (setter): `rv_dom_html_template_element_set_shadow_root_reference_target`.

### HTMLTextAreaElement

- `dirName` (getter): `rv_dom_html_text_area_element_get_dir_name`.
- `dirName` (setter): `rv_dom_html_text_area_element_set_dir_name`.
- `disabled` (getter): `rv_dom_html_text_area_element_get_disabled`.
- `disabled` (setter): `rv_dom_html_text_area_element_set_disabled`.
- `form` (getter): `rv_dom_html_text_area_element_get_form`.
- `minLength` (getter): `rv_dom_html_text_area_element_get_min_length`.
- `minLength` (setter): `rv_dom_html_text_area_element_set_min_length`.
- `maxLength` (getter): `rv_dom_html_text_area_element_get_max_length`.
- `maxLength` (setter): `rv_dom_html_text_area_element_set_max_length`.
- `name` (getter): `rv_dom_html_text_area_element_get_name`.
- `name` (setter): `rv_dom_html_text_area_element_set_name`.
- `placeholder` (getter): `rv_dom_html_text_area_element_get_placeholder`.
- `placeholder` (setter): `rv_dom_html_text_area_element_set_placeholder`.
- `readOnly` (getter): `rv_dom_html_text_area_element_get_read_only`.
- `readOnly` (setter): `rv_dom_html_text_area_element_set_read_only`.
- `required` (getter): `rv_dom_html_text_area_element_get_required`.
- `required` (setter): `rv_dom_html_text_area_element_set_required`.
- `rows` (getter): `rv_dom_html_text_area_element_get_rows`.
- `rows` (setter): `rv_dom_html_text_area_element_set_rows`.
- `cols` (getter): `rv_dom_html_text_area_element_get_cols`.
- `cols` (setter): `rv_dom_html_text_area_element_set_cols`.
- `wrap` (getter): `rv_dom_html_text_area_element_get_wrap`.
- `wrap` (setter): `rv_dom_html_text_area_element_set_wrap`.
- `type` (getter): `rv_dom_html_text_area_element_get_type`.
- `defaultValue` (getter): `rv_dom_html_text_area_element_get_default_value`.
- `defaultValue` (setter): `rv_dom_html_text_area_element_set_default_value`.
- `value` (getter): `rv_dom_html_text_area_element_get_value`.
- `value` (setter): `rv_dom_html_text_area_element_set_value`.
- `textLength` (getter): `rv_dom_html_text_area_element_get_text_length`.
- `willValidate` (getter): `rv_dom_html_text_area_element_get_will_validate`.
- `validity` (getter): `rv_dom_html_text_area_element_get_validity`.
- `validationMessage` (getter): `rv_dom_html_text_area_element_get_validation_message`.
- `labels` (getter): `rv_dom_html_text_area_element_get_labels`.
- `selectionStart` (getter): `rv_dom_html_text_area_element_get_selection_start`.
- `selectionStart` (setter): `rv_dom_html_text_area_element_set_selection_start`.
- `selectionEnd` (getter): `rv_dom_html_text_area_element_get_selection_end`.
- `selectionEnd` (setter): `rv_dom_html_text_area_element_set_selection_end`.
- `selectionDirection` (getter): `rv_dom_html_text_area_element_get_selection_direction`.
- `selectionDirection` (setter): `rv_dom_html_text_area_element_set_selection_direction`.
- `autocomplete` (getter): `rv_dom_html_text_area_element_get_autocomplete`.
- `autocomplete` (setter): `rv_dom_html_text_area_element_set_autocomplete`.
- `checkValidity` (operation): `rv_dom_html_text_area_element_check_validity`.
- `reportValidity` (operation): `rv_dom_html_text_area_element_report_validity`.
- `setCustomValidity` (operation): `rv_dom_html_text_area_element_set_custom_validity`.
- `select` (operation): `rv_dom_html_text_area_element_select`.
- `setRangeText` (operation): `rv_dom_html_text_area_element_set_range_text_1`.
- `setRangeText` (operation): `rv_dom_html_text_area_element_set_range_text_4`.
- `setSelectionRange` (operation): `rv_dom_html_text_area_element_set_selection_range`.

### HTMLUListElement

- `compact` (getter): `rv_dom_htmlu_list_element_get_compact`.
- `compact` (setter): `rv_dom_htmlu_list_element_set_compact`.
- `type` (getter): `rv_dom_htmlu_list_element_get_type`.
- `type` (setter): `rv_dom_htmlu_list_element_set_type`.

### InputEvent

- `inputType` (getter): `rv_dom_input_event_get_input_type`.
- `data` (getter): `rv_dom_input_event_get_data`.
- `isComposing` (getter): `rv_dom_input_event_get_is_composing`.
- `getTargetRanges` (operation): `rv_dom_input_event_get_target_ranges`.

### KeyboardEvent

- `key` (getter): `rv_dom_keyboard_event_get_key`.
- `code` (getter): `rv_dom_keyboard_event_get_code`.
- `location` (getter): `rv_dom_keyboard_event_get_location`.
- `ctrlKey` (getter): `rv_dom_keyboard_event_get_ctrl_key`.
- `shiftKey` (getter): `rv_dom_keyboard_event_get_shift_key`.
- `altKey` (getter): `rv_dom_keyboard_event_get_alt_key`.
- `metaKey` (getter): `rv_dom_keyboard_event_get_meta_key`.
- `repeat` (getter): `rv_dom_keyboard_event_get_repeat`.
- `isComposing` (getter): `rv_dom_keyboard_event_get_is_composing`.
- `keyIdentifier` (getter): `rv_dom_keyboard_event_get_key_identifier`.
- `charCode` (getter): `rv_dom_keyboard_event_get_char_code`.
- `keyCode` (getter): `rv_dom_keyboard_event_get_key_code`.
- `getModifierState` (operation): `rv_dom_keyboard_event_get_modifier_state`.

### MouseEvent

- `screenX` (getter): `rv_dom_mouse_event_get_screen_x`.
- `screenY` (getter): `rv_dom_mouse_event_get_screen_y`.
- `clientX` (getter): `rv_dom_mouse_event_get_client_x`.
- `clientY` (getter): `rv_dom_mouse_event_get_client_y`.
- `ctrlKey` (getter): `rv_dom_mouse_event_get_ctrl_key`.
- `shiftKey` (getter): `rv_dom_mouse_event_get_shift_key`.
- `altKey` (getter): `rv_dom_mouse_event_get_alt_key`.
- `metaKey` (getter): `rv_dom_mouse_event_get_meta_key`.
- `button` (getter): `rv_dom_mouse_event_get_button`.
- `buttons` (getter): `rv_dom_mouse_event_get_buttons`.
- `relatedTarget` (getter): `rv_dom_mouse_event_get_related_target`.
- `movementX` (getter): `rv_dom_mouse_event_get_movement_x`.
- `movementY` (getter): `rv_dom_mouse_event_get_movement_y`.
- `offsetX` (getter): `rv_dom_mouse_event_get_offset_x`.
- `offsetY` (getter): `rv_dom_mouse_event_get_offset_y`.
- `x` (getter): `rv_dom_mouse_event_get_x`.
- `y` (getter): `rv_dom_mouse_event_get_y`.
- `fromElement` (getter): `rv_dom_mouse_event_get_from_element`.
- `toElement` (getter): `rv_dom_mouse_event_get_to_element`.
- `getModifierState` (operation): `rv_dom_mouse_event_get_modifier_state`.

### NamedNodeMap

- `length` (getter): `rv_dom_named_node_map_get_length`.
- `item` (operation): `rv_dom_named_node_map_item`.
- `getNamedItem` (operation): `rv_dom_named_node_map_get_named_item`.
- `getNamedItemNS` (operation): `rv_dom_named_node_map_get_named_item_ns`.
- `setNamedItem` (operation): `rv_dom_named_node_map_set_named_item`.
- `setNamedItemNS` (operation): `rv_dom_named_node_map_set_named_item_ns`.
- `removeNamedItem` (operation): `rv_dom_named_node_map_remove_named_item`.
- `removeNamedItemNS` (operation): `rv_dom_named_node_map_remove_named_item_ns`.

### Node

- `nodeType` (getter): `rv_dom_node_get_node_type`.
- `nodeName` (getter): `rv_dom_node_get_node_name`.
- `baseURI` (getter): `rv_dom_node_get_base_uri`.
- `isConnected` (getter): `rv_dom_node_get_is_connected`.
- `ownerDocument` (getter): `rv_dom_node_get_owner_document`.
- `parentNode` (getter): `rv_dom_node_get_parent_node`.
- `parentElement` (getter): `rv_dom_node_get_parent_element`.
- `childNodes` (getter): `rv_dom_node_get_child_nodes`.
- `firstChild` (getter): `rv_dom_node_get_first_child`.
- `lastChild` (getter): `rv_dom_node_get_last_child`.
- `previousSibling` (getter): `rv_dom_node_get_previous_sibling`.
- `nextSibling` (getter): `rv_dom_node_get_next_sibling`.
- `nodeValue` (getter): `rv_dom_node_get_node_value`.
- `nodeValue` (setter): `rv_dom_node_set_node_value`.
- `textContent` (getter): `rv_dom_node_get_text_content`.
- `textContent` (setter): `rv_dom_node_set_text_content`.
- `getRootNode` (operation): `rv_dom_node_get_root_node`.
- `hasChildNodes` (operation): `rv_dom_node_has_child_nodes`.
- `normalize` (operation): `rv_dom_node_normalize`.
- `cloneNode` (operation): `rv_dom_node_clone_node`.
- `isEqualNode` (operation): `rv_dom_node_is_equal_node`.
- `isSameNode` (operation): `rv_dom_node_is_same_node`.
- `compareDocumentPosition` (operation): `rv_dom_node_compare_document_position`.
- `contains` (operation): `rv_dom_node_contains`.
- `lookupPrefix` (operation): `rv_dom_node_lookup_prefix`.
- `lookupNamespaceURI` (operation): `rv_dom_node_lookup_namespace_uri`.
- `isDefaultNamespace` (operation): `rv_dom_node_is_default_namespace`.
- `insertBefore` (operation): `rv_dom_node_insert_before`.
- `appendChild` (operation): `rv_dom_node_append_child`.
- `replaceChild` (operation): `rv_dom_node_replace_child`.
- `removeChild` (operation): `rv_dom_node_remove_child`.

### NodeList

- `length` (getter): `rv_dom_node_list_get_length`.
- `item` (operation): `rv_dom_node_list_item`.

### PointerEvent

- `pointerId` (getter): `rv_dom_pointer_event_get_pointer_id`.
- `width` (getter): `rv_dom_pointer_event_get_width`.
- `height` (getter): `rv_dom_pointer_event_get_height`.
- `pressure` (getter): `rv_dom_pointer_event_get_pressure`.
- `tangentialPressure` (getter): `rv_dom_pointer_event_get_tangential_pressure`.
- `tiltX` (getter): `rv_dom_pointer_event_get_tilt_x`.
- `tiltY` (getter): `rv_dom_pointer_event_get_tilt_y`.
- `twist` (getter): `rv_dom_pointer_event_get_twist`.
- `altitudeAngle` (getter): `rv_dom_pointer_event_get_altitude_angle`.
- `azimuthAngle` (getter): `rv_dom_pointer_event_get_azimuth_angle`.
- `pointerType` (getter): `rv_dom_pointer_event_get_pointer_type`.
- `isPrimary` (getter): `rv_dom_pointer_event_get_is_primary`.
- `getCoalescedEvents` (operation): `rv_dom_pointer_event_get_coalesced_events`.
- `getPredictedEvents` (operation): `rv_dom_pointer_event_get_predicted_events`.

### RadioNodeList

- `value` (getter): `rv_dom_radio_node_list_get_value`.
- `value` (setter): `rv_dom_radio_node_list_set_value`.

### Range

- `commonAncestorContainer` (getter): `rv_dom_range_get_common_ancestor_container`.
- `setStart` (operation): `rv_dom_range_set_start`.
- `setEnd` (operation): `rv_dom_range_set_end`.
- `setStartBefore` (operation): `rv_dom_range_set_start_before`.
- `setStartAfter` (operation): `rv_dom_range_set_start_after`.
- `setEndBefore` (operation): `rv_dom_range_set_end_before`.
- `setEndAfter` (operation): `rv_dom_range_set_end_after`.
- `collapse` (operation): `rv_dom_range_collapse`.
- `selectNode` (operation): `rv_dom_range_select_node`.
- `selectNodeContents` (operation): `rv_dom_range_select_node_contents`.
- `compareBoundaryPoints` (operation): `rv_dom_range_compare_boundary_points`.
- `deleteContents` (operation): `rv_dom_range_delete_contents`.
- `extractContents` (operation): `rv_dom_range_extract_contents`.
- `cloneContents` (operation): `rv_dom_range_clone_contents`.
- `insertNode` (operation): `rv_dom_range_insert_node`.
- `surroundContents` (operation): `rv_dom_range_surround_contents`.
- `cloneRange` (operation): `rv_dom_range_clone_range`.
- `detach` (operation): `rv_dom_range_detach`.
- `isPointInRange` (operation): `rv_dom_range_is_point_in_range`.
- `comparePoint` (operation): `rv_dom_range_compare_point`.
- `intersectsNode` (operation): `rv_dom_range_intersects_node`.
- `expand` (operation): `rv_dom_range_expand`.
- `compareNode` (operation): `rv_dom_range_compare_node`.
- `getBoundingClientRect` (operation): `rv_dom_range_get_bounding_client_rect`.
- `createContextualFragment` (operation): `rv_dom_range_create_contextual_fragment`.

### ShadowRoot

- `mode` (getter): `rv_dom_shadow_root_get_mode`.
- `delegatesFocus` (getter): `rv_dom_shadow_root_get_delegates_focus`.
- `slotAssignment` (getter): `rv_dom_shadow_root_get_slot_assignment`.
- `clonable` (getter): `rv_dom_shadow_root_get_clonable`.
- `serializable` (getter): `rv_dom_shadow_root_get_serializable`.
- `host` (getter): `rv_dom_shadow_root_get_host`.
- `referenceTarget` (getter): `rv_dom_shadow_root_get_reference_target`.
- `referenceTarget` (setter): `rv_dom_shadow_root_set_reference_target`.
- `activeElement` (getter): `rv_dom_shadow_root_get_active_element`.
- `fullscreenElement` (getter): `rv_dom_shadow_root_get_fullscreen_element`.
- `pointerLockElement` (getter): `rv_dom_shadow_root_get_pointer_lock_element`.
- `innerHTML` (getter): `rv_dom_shadow_root_get_inner_html`.
- `innerHTML` (setter): `rv_dom_shadow_root_set_inner_html`.
- `elementFromPoint` (operation): `rv_dom_shadow_root_element_from_point`.
- `elementsFromPoint` (operation): `rv_dom_shadow_root_elements_from_point`.
- `setHTMLUnsafe` (operation): `rv_dom_shadow_root_set_html_unsafe`.
- `getHTML` (operation): `rv_dom_shadow_root_get_html`.

### Text

- `wholeText` (getter): `rv_dom_text_get_whole_text`.
- `assignedSlot` (getter): `rv_dom_text_get_assigned_slot`.
- `splitText` (operation): `rv_dom_text_split_text`.

### UIEvent

- `detail` (getter): `rv_dom_ui_event_get_detail`.
- `layerX` (getter): `rv_dom_ui_event_get_layer_x`.
- `layerY` (getter): `rv_dom_ui_event_get_layer_y`.
- `pageX` (getter): `rv_dom_ui_event_get_page_x`.
- `pageY` (getter): `rv_dom_ui_event_get_page_y`.
- `which` (getter): `rv_dom_ui_event_get_which`.

### ValidityState

- `valueMissing` (getter): `rv_dom_validity_state_get_value_missing`.
- `typeMismatch` (getter): `rv_dom_validity_state_get_type_mismatch`.
- `patternMismatch` (getter): `rv_dom_validity_state_get_pattern_mismatch`.
- `tooShort` (getter): `rv_dom_validity_state_get_too_short`.
- `tooLong` (getter): `rv_dom_validity_state_get_too_long`.
- `rangeUnderflow` (getter): `rv_dom_validity_state_get_range_underflow`.
- `rangeOverflow` (getter): `rv_dom_validity_state_get_range_overflow`.
- `stepMismatch` (getter): `rv_dom_validity_state_get_step_mismatch`.
- `badInput` (getter): `rv_dom_validity_state_get_bad_input`.
- `customError` (getter): `rv_dom_validity_state_get_custom_error`.
- `valid` (getter): `rv_dom_validity_state_get_valid`.

### WheelEvent

- `deltaX` (getter): `rv_dom_wheel_event_get_delta_x`.
- `deltaY` (getter): `rv_dom_wheel_event_get_delta_y`.
- `deltaZ` (getter): `rv_dom_wheel_event_get_delta_z`.
- `deltaMode` (getter): `rv_dom_wheel_event_get_delta_mode`.
- `wheelDeltaX` (getter): `rv_dom_wheel_event_get_wheel_delta_x`.
- `wheelDeltaY` (getter): `rv_dom_wheel_event_get_wheel_delta_y`.
- `wheelDelta` (getter): `rv_dom_wheel_event_get_wheel_delta`.

## Current native generator omission report

Copied as diagnostic evidence from the source baseline. An omitted handler property is not necessarily a missing event capability, and an omitted generated operation can have a legacy equivalent. This report is not the candidate selection list.

```text
Generated by gen/domgen.py: the members of gen/dom.json's interfaces, and those left out with the reason.

interfaces 62, functions 1070 (getters 539, setters 279, operations 252), handle types 11, constants 37, enumerations 8, dictionaries 14, stubs 0, left out 444

Functions per interface:
  Node: 31
  CharacterData: 14
  Text: 3
  Comment: 0
  DocumentFragment: 11
  ShadowRoot: 17
  Document: 63
  HTMLDocument: 0
  DOMImplementation: 2
  Element: 177
  Attr: 9
  NamedNodeMap: 8
  HTMLElement: 65
  HTMLAnchorElement: 46
  HTMLBodyElement: 12
  HTMLButtonElement: 34
  HTMLDetailsElement: 4
  HTMLDialogElement: 10
  HTMLDivElement: 2
  HTMLFieldSetElement: 13
  HTMLFormElement: 28
  HTMLHeadElement: 0
  HTMLImageElement: 49
  HTMLInputElement: 101
  HTMLLabelElement: 4
  HTMLLegendElement: 3
  HTMLLIElement: 4
  HTMLMeterElement: 13
  HTMLOListElement: 8
  HTMLOptGroupElement: 4
  HTMLOptionElement: 14
  HTMLOutputElement: 16
  HTMLParagraphElement: 2
  HTMLProgressElement: 6
  HTMLSelectElement: 35
  HTMLSlotElement: 5
  HTMLSpanElement: 0
  HTMLTextAreaElement: 48
  HTMLTemplateElement: 15
  HTMLUListElement: 4
  CSSStyleDeclaration: 8
  DOMTokenList: 10
  DOMStringMap: 3
  AbstractRange: 5
  StaticRange: 0
  Range: 25
  DOMSelection: 29
  DOMRectReadOnly: 8
  DOMRect: 0
  NodeList: 2
  RadioNodeList: 2
  HTMLCollection: 3
  ValidityState: 11
  Event: 18
  UIEvent: 6
  MouseEvent: 20
  PointerEvent: 14
  WheelEvent: 7
  KeyboardEvent: 13
  FocusEvent: 1
  InputEvent: 4
  CompositionEvent: 1

Stubs, for members of the lock that WebKit's IDL no longer has:
  (none)

Left out:
  AddEventListenerOptions.signal: excluded dictionary field by gen/dom.json
  AddEventListenerOptions.webkitTrustedOnly: excluded dictionary field by gen/dom.json
  CSSStyleDeclaration.cssRules: interface CSSRuleList outside gen/dom.json
  CSSStyleDeclaration.getPropertyCSSValue: interface DeprecatedCSSOMValue outside gen/dom.json
  CSSStyleDeclaration.parentRule: interface CSSRule outside gen/dom.json
  Comment.constructor: constructor
  CompositionEvent.constructor: constructor
  CompositionEvent.initCompositionEvent: type WindowProxy?
  DOMImplementation.createDocument: interface DocumentType outside gen/dom.json
  DOMImplementation.createDocumentType: interface DocumentType outside gen/dom.json
  DOMRect.constructor: constructor
  DOMRect.fromRect: static
  DOMRect.height: excluded by gen/dom.json
  DOMRect.width: excluded by gen/dom.json
  DOMRect.x: excluded by gen/dom.json
  DOMRect.y: excluded by gen/dom.json
  DOMRectReadOnly.constructor: constructor
  DOMRectReadOnly.fromRect: static
  DOMRectReadOnly.toJSON: type object
  Document.URL: excluded by gen/dom.json
  Document.activeViewTransition: interface ViewTransition outside gen/dom.json
  Document.adoptedStyleSheets: type any
  Document.alinkColor: excluded by gen/dom.json
  Document.all: interface HTMLAllCollection outside gen/dom.json
  Document.applets: excluded by gen/dom.json
  Document.bgColor: excluded by gen/dom.json
  Document.captureEvents: excluded by gen/dom.json
  Document.caretPositionFromPoint: interface CaretPosition outside gen/dom.json
  Document.charset: excluded by gen/dom.json
  Document.clear: excluded by gen/dom.json
  Document.close: excluded by gen/dom.json
  Document.constructor: constructor
  Document.cookie: excluded by gen/dom.json
  Document.createCDATASection: interface CDATASection outside gen/dom.json
  Document.createExpression: interface XPathNSResolver outside gen/dom.json
  Document.createNSResolver: excluded by gen/dom.json
  Document.createNodeIterator: callback NodeFilter
  Document.createProcessingInstruction: interface ProcessingInstruction outside gen/dom.json
  Document.createTouch: type WindowProxy?
  Document.createTouchList: interface Touch outside gen/dom.json
  Document.createTreeWalker: callback NodeFilter
  Document.currentScript: excluded by gen/dom.json
  Document.customElementRegistry: interface CustomElementRegistry outside gen/dom.json
  Document.defaultView: excluded by gen/dom.json
  Document.doctype: interface DocumentType outside gen/dom.json
  Document.documentURI: excluded by gen/dom.json
  Document.domain: excluded by gen/dom.json
  Document.embeds: excluded by gen/dom.json
  Document.evaluate: interface XPathNSResolver outside gen/dom.json
  Document.exitFullscreen: excluded by gen/dom.json
  Document.exitImmersive: Promise type Promise<undefined>
  Document.exitPointerLock: excluded by gen/dom.json
  Document.fgColor: excluded by gen/dom.json
  Document.fonts: interface FontFaceSet outside gen/dom.json
  Document.fragmentDirective: interface FragmentDirective outside gen/dom.json
  Document.fullscreen: excluded by gen/dom.json
  Document.fullscreenElement: excluded by gen/dom.json
  Document.fullscreenEnabled: excluded by gen/dom.json
  Document.getAnimations: interface WebAnimation outside gen/dom.json
  Document.getCSSCanvasContext: interface GPUCanvasContext outside gen/dom.json
  Document.hasStorageAccess: excluded by gen/dom.json
  Document.immersiveElement: excluded by gen/dom.json
  Document.immersiveEnabled: excluded by gen/dom.json
  Document.inputEncoding: excluded by gen/dom.json
  Document.lastModified: excluded by gen/dom.json
  Document.linkColor: excluded by gen/dom.json
  Document.location: excluded by gen/dom.json
  Document.onabort: callback EventHandler
  Document.onanimationcancel: callback EventHandler
  Document.onanimationend: callback EventHandler
  Document.onanimationiteration: callback EventHandler
  Document.onanimationstart: callback EventHandler
  Document.onauxclick: callback EventHandler
  Document.onbeforecopy: callback EventHandler
  Document.onbeforecut: callback EventHandler
  Document.onbeforeinput: callback EventHandler
  Document.onbeforematch: callback EventHandler
  Document.onbeforepaste: callback EventHandler
  Document.onbeforetoggle: callback EventHandler
  Document.onblur: callback EventHandler
  Document.oncancel: callback EventHandler
  Document.oncanplay: callback EventHandler
  Document.oncanplaythrough: callback EventHandler
  Document.onchange: callback EventHandler
  Document.onclick: callback EventHandler
  Document.onclose: callback EventHandler
  Document.oncommand: callback EventHandler
  Document.oncontentvisibilityautostatechange: callback EventHandler
  Document.oncontextmenu: callback EventHandler
  Document.oncopy: callback EventHandler
  Document.oncuechange: callback EventHandler
  Document.oncut: callback EventHandler
  Document.ondblclick: callback EventHandler
  Document.ondrag: callback EventHandler
  Document.ondragend: callback EventHandler
  Document.ondragenter: callback EventHandler
  Document.ondragleave: callback EventHandler
  Document.ondragover: callback EventHandler
  Document.ondragstart: callback EventHandler
  Document.ondrop: callback EventHandler
  Document.ondurationchange: callback EventHandler
  Document.onemptied: callback EventHandler
  Document.onended: callback EventHandler
  Document.onerror: callback OnErrorEventHandler
  Document.onfocus: callback EventHandler
  Document.onformdata: callback EventHandler
  Document.onfullscreenchange: callback EventHandler
  Document.onfullscreenerror: callback EventHandler
  Document.ongotpointercapture: callback EventHandler
  Document.onimmersivechange: callback EventHandler
  Document.onimmersiveerror: callback EventHandler
  Document.oninput: callback EventHandler
  Document.oninvalid: callback EventHandler
  Document.onkeydown: callback EventHandler
  Document.onkeypress: callback EventHandler
  Document.onkeyup: callback EventHandler
  Document.onload: callback EventHandler
  Document.onloadeddata: callback EventHandler
  Document.onloadedmetadata: callback EventHandler
  Document.onloadstart: callback EventHandler
  Document.onlostpointercapture: callback EventHandler
  Document.onmousedown: callback EventHandler
  Document.onmouseenter: callback EventHandler
  Document.onmouseleave: callback EventHandler
  Document.onmousemove: callback EventHandler
  Document.onmouseout: callback EventHandler
  Document.onmouseover: callback EventHandler
  Document.onmouseup: callback EventHandler
  Document.onmousewheel: callback EventHandler
  Document.onpaste: callback EventHandler
  Document.onpause: callback EventHandler
  Document.onplay: callback EventHandler
  Document.onplaying: callback EventHandler
  Document.onpointercancel: callback EventHandler
  Document.onpointerdown: callback EventHandler
  Document.onpointerenter: callback EventHandler
  Document.onpointerleave: callback EventHandler
  Document.onpointerlockchange: callback EventHandler
  Document.onpointerlockerror: callback EventHandler
  Document.onpointermove: callback EventHandler
  Document.onpointerout: callback EventHandler
  Document.onpointerover: callback EventHandler
  Document.onpointerup: callback EventHandler
  Document.onprogress: callback EventHandler
  Document.onratechange: callback EventHandler
  Document.onreadystatechange: callback EventHandler
  Document.onreset: callback EventHandler
  Document.onresize: callback EventHandler
  Document.onscroll: callback EventHandler
  Document.onscrollend: callback EventHandler
  Document.onsecuritypolicyviolation: callback EventHandler
  Document.onseeked: callback EventHandler
  Document.onseeking: callback EventHandler
  Document.onselect: callback EventHandler
  Document.onselectionchange: callback EventHandler
  Document.onselectstart: callback EventHandler
  Document.onslotchange: callback EventHandler
  Document.onstalled: callback EventHandler
  Document.onsubmit: callback EventHandler
  Document.onsuspend: callback EventHandler
  Document.ontimeupdate: callback EventHandler
  Document.ontoggle: callback EventHandler
  Document.ontouchcancel: callback EventHandler
  Document.ontouchend: callback EventHandler
  Document.ontouchforcechange: callback EventHandler
  Document.ontouchmove: callback EventHandler
  Document.ontouchstart: callback EventHandler
  Document.ontransitioncancel: callback EventHandler
  Document.ontransitionend: callback EventHandler
  Document.ontransitionrun: callback EventHandler
  Document.ontransitionstart: callback EventHandler
  Document.onvisibilitychange: callback EventHandler
  Document.onvolumechange: callback EventHandler
  Document.onwaiting: callback EventHandler
  Document.onwebkitanimationend: WebKit-prefixed
  Document.onwebkitanimationiteration: WebKit-prefixed
  Document.onwebkitanimationstart: WebKit-prefixed
  Document.onwebkitfullscreenchange: WebKit-prefixed
  Document.onwebkitfullscreenerror: WebKit-prefixed
  Document.onwebkitmouseforcechanged: WebKit-prefixed
  Document.onwebkitmouseforcedown: WebKit-prefixed
  Document.onwebkitmouseforceup: WebKit-prefixed
  Document.onwebkitmouseforcewillbegin: WebKit-prefixed
  Document.onwebkittransitionend: WebKit-prefixed
  Document.onwheel: callback EventHandler
  Document.open: excluded by gen/dom.json
  Document.open: excluded by gen/dom.json
  Document.parseHTMLUnsafe: static
  Document.plugins: excluded by gen/dom.json
  Document.pointerLockElement: excluded by gen/dom.json
  Document.referrer: excluded by gen/dom.json
  Document.releaseEvents: excluded by gen/dom.json
  Document.requestStorageAccess: excluded by gen/dom.json
  Document.rootElement: interface SVGSVGElement outside gen/dom.json
  Document.startViewTransition: callback ViewTransitionUpdateCallback
  Document.styleSheets: interface StyleSheetList outside gen/dom.json
  Document.timeline: interface DocumentTimeline outside gen/dom.json
  Document.undoManager: interface UndoManager outside gen/dom.json
  Document.vlinkColor: excluded by gen/dom.json
  Document.webkitCancelFullScreen: WebKit-prefixed
  Document.webkitCurrentFullScreenElement: WebKit-prefixed
  Document.webkitExitFullscreen: WebKit-prefixed
  Document.webkitFullScreenKeyboardInputAllowed: WebKit-prefixed
  Document.webkitFullscreenElement: WebKit-prefixed
  Document.webkitFullscreenEnabled: WebKit-prefixed
  Document.webkitIsFullScreen: WebKit-prefixed
  Document.write: excluded by gen/dom.json
  Document.writeln: excluded by gen/dom.json
  Document.xmlEncoding: excluded by gen/dom.json
  Document.xmlStandalone: excluded by gen/dom.json
  Document.xmlVersion: excluded by gen/dom.json
  DocumentFragment.constructor: constructor
  Element.animate: type object?
  Element.ariaActionsElements: [CustomGetter]
  Element.ariaActionsElements: setter: [Reflect] of type FrozenArray
  Element.ariaControlsElements: [CustomGetter]
  Element.ariaControlsElements: setter: [Reflect] of type FrozenArray
  Element.ariaDescribedByElements: [CustomGetter]
  Element.ariaDescribedByElements: setter: [Reflect] of type FrozenArray
  Element.ariaDetailsElements: [CustomGetter]
  Element.ariaDetailsElements: setter: [Reflect] of type FrozenArray
  Element.ariaErrorMessageElements: [CustomGetter]
  Element.ariaErrorMessageElements: setter: [Reflect] of type FrozenArray
  Element.ariaFlowToElements: [CustomGetter]
  Element.ariaFlowToElements: setter: [Reflect] of type FrozenArray
  Element.ariaLabelledByElements: [CustomGetter]
  Element.ariaLabelledByElements: setter: [Reflect] of type FrozenArray
  Element.ariaOwnsElements: [CustomGetter]
  Element.ariaOwnsElements: setter: [Reflect] of type FrozenArray
  Element.computedStyleMap: interface StylePropertyMapReadOnly outside gen/dom.json
  Element.customElementRegistry: interface CustomElementRegistry outside gen/dom.json
  Element.getAnimations: interface WebAnimation outside gen/dom.json
  Element.getClientRects: interface DOMRectList outside gen/dom.json
  Element.onbeforeload: callback EventHandler
  Element.onfocusin: callback EventHandler
  Element.onfocusout: callback EventHandler
  Element.onfullscreenchange: callback EventHandler
  Element.onfullscreenerror: callback EventHandler
  Element.ongesturechange: callback EventHandler
  Element.ongestureend: callback EventHandler
  Element.ongesturestart: callback EventHandler
  Element.onwebkitcurrentplaybacktargetiswirelesschanged: WebKit-prefixed
  Element.onwebkitfullscreenchange: WebKit-prefixed
  Element.onwebkitfullscreenerror: WebKit-prefixed
  Element.onwebkitneedkey: WebKit-prefixed
  Element.onwebkitplaybacktargetavailabilitychanged: WebKit-prefixed
  Element.onwebkitpresentationmodechanged: WebKit-prefixed
  Element.openOrClosedShadowRoot: [EnabledForWorld]
  Element.requestFullscreen: excluded by gen/dom.json
  Element.requestPointerLock: excluded by gen/dom.json
  Element.setUserInfo: [EnabledForWorld]
  Element.shadowRoot: getter: [CallWith=CurrentGlobalObject]
  Element.webkitMatchesSelector: WebKit-prefixed
  Element.webkitRequestFullScreen: WebKit-prefixed
  Element.webkitRequestFullscreen: WebKit-prefixed
  ElementCreationOptions.customElementRegistry: excluded dictionary field by gen/dom.json
  Event.composedPath: [CallWith=CurrentGlobalObject]
  Event.constructor: constructor
  Event.timeStamp: getter: [CallWith] on an interface other than a node
  FocusEvent.constructor: constructor
  HTMLAnchorElement.attributionDestination: excluded by gen/dom.json
  HTMLAnchorElement.attributionSourceId: excluded by gen/dom.json
  HTMLAnchorElement.attributionSourceNonce: excluded by gen/dom.json
  HTMLAnchorElement.ping: excluded by gen/dom.json
  HTMLBodyElement.onafterprint: callback EventHandler
  HTMLBodyElement.onbeforeprint: callback EventHandler
  HTMLBodyElement.onbeforeunload: callback OnBeforeUnloadEventHandler
  HTMLBodyElement.onblur: callback EventHandler
  HTMLBodyElement.onerror: callback OnErrorEventHandler
  HTMLBodyElement.onfocus: callback EventHandler
  HTMLBodyElement.onfocusin: callback EventHandler
  HTMLBodyElement.onfocusout: callback EventHandler
  HTMLBodyElement.onhashchange: callback EventHandler
  HTMLBodyElement.onlanguagechange: callback EventHandler
  HTMLBodyElement.onload: callback EventHandler
  HTMLBodyElement.onmessage: callback EventHandler
  HTMLBodyElement.onmessageerror: callback EventHandler
  HTMLBodyElement.onoffline: callback EventHandler
  HTMLBodyElement.ononline: callback EventHandler
  HTMLBodyElement.onorientationchange: callback EventHandler
  HTMLBodyElement.onpagehide: callback EventHandler
  HTMLBodyElement.onpagereveal: callback EventHandler
  HTMLBodyElement.onpageshow: callback EventHandler
  HTMLBodyElement.onpageswap: callback EventHandler
  HTMLBodyElement.onpopstate: callback EventHandler
  HTMLBodyElement.onrejectionhandled: callback EventHandler
  HTMLBodyElement.onresize: callback EventHandler
  HTMLBodyElement.onscroll: callback EventHandler
  HTMLBodyElement.onstorage: callback EventHandler
  HTMLBodyElement.onunhandledrejection: callback EventHandler
  HTMLBodyElement.onunload: callback EventHandler
  HTMLBodyElement.onwebkitmouseforcechanged: WebKit-prefixed
  HTMLBodyElement.onwebkitmouseforcedown: WebKit-prefixed
  HTMLBodyElement.onwebkitmouseforceup: WebKit-prefixed
  HTMLBodyElement.onwebkitmouseforcewillbegin: WebKit-prefixed
  HTMLDocument.namedItem: type WindowProxy
  HTMLElement.attachInternals: interface ElementInternals outside gen/dom.json
  HTMLElement.attributeStyleMap: interface StylePropertyMap outside gen/dom.json
  HTMLElement.constructor: constructor
  HTMLElement.onabort: callback EventHandler
  HTMLElement.onanimationcancel: callback EventHandler
  HTMLElement.onanimationend: callback EventHandler
  HTMLElement.onanimationiteration: callback EventHandler
  HTMLElement.onanimationstart: callback EventHandler
  HTMLElement.onauxclick: callback EventHandler
  HTMLElement.onbeforecopy: callback EventHandler
  HTMLElement.onbeforecut: callback EventHandler
  HTMLElement.onbeforeinput: callback EventHandler
  HTMLElement.onbeforematch: callback EventHandler
  HTMLElement.onbeforepaste: callback EventHandler
  HTMLElement.onbeforetoggle: callback EventHandler
  HTMLElement.onblur: callback EventHandler
  HTMLElement.oncancel: callback EventHandler
  HTMLElement.oncanplay: callback EventHandler
  HTMLElement.oncanplaythrough: callback EventHandler
  HTMLElement.onchange: callback EventHandler
  HTMLElement.onclick: callback EventHandler
  HTMLElement.onclose: callback EventHandler
  HTMLElement.oncommand: callback EventHandler
  HTMLElement.oncontentvisibilityautostatechange: callback EventHandler
  HTMLElement.oncontextmenu: callback EventHandler
  HTMLElement.oncopy: callback EventHandler
  HTMLElement.oncuechange: callback EventHandler
  HTMLElement.oncut: callback EventHandler
  HTMLElement.ondblclick: callback EventHandler
  HTMLElement.ondrag: callback EventHandler
  HTMLElement.ondragend: callback EventHandler
  HTMLElement.ondragenter: callback EventHandler
  HTMLElement.ondragleave: callback EventHandler
  HTMLElement.ondragover: callback EventHandler
  HTMLElement.ondragstart: callback EventHandler
  HTMLElement.ondrop: callback EventHandler
  HTMLElement.ondurationchange: callback EventHandler
  HTMLElement.onemptied: callback EventHandler
  HTMLElement.onended: callback EventHandler
  HTMLElement.onerror: callback OnErrorEventHandler
  HTMLElement.onfocus: callback EventHandler
  HTMLElement.onformdata: callback EventHandler
  HTMLElement.ongotpointercapture: callback EventHandler
  HTMLElement.oninput: callback EventHandler
  HTMLElement.oninvalid: callback EventHandler
  HTMLElement.onkeydown: callback EventHandler
  HTMLElement.onkeypress: callback EventHandler
  HTMLElement.onkeyup: callback EventHandler
  HTMLElement.onload: callback EventHandler
  HTMLElement.onloadeddata: callback EventHandler
  HTMLElement.onloadedmetadata: callback EventHandler
  HTMLElement.onloadstart: callback EventHandler
  HTMLElement.onlostpointercapture: callback EventHandler
  HTMLElement.onmousedown: callback EventHandler
  HTMLElement.onmouseenter: callback EventHandler
  HTMLElement.onmouseleave: callback EventHandler
  HTMLElement.onmousemove: callback EventHandler
  HTMLElement.onmouseout: callback EventHandler
  HTMLElement.onmouseover: callback EventHandler
  HTMLElement.onmouseup: callback EventHandler
  HTMLElement.onmousewheel: callback EventHandler
  HTMLElement.onpaste: callback EventHandler
  HTMLElement.onpause: callback EventHandler
  HTMLElement.onplay: callback EventHandler
  HTMLElement.onplaying: callback EventHandler
  HTMLElement.onpointercancel: callback EventHandler
  HTMLElement.onpointerdown: callback EventHandler
  HTMLElement.onpointerenter: callback EventHandler
  HTMLElement.onpointerleave: callback EventHandler
  HTMLElement.onpointermove: callback EventHandler
  HTMLElement.onpointerout: callback EventHandler
  HTMLElement.onpointerover: callback EventHandler
  HTMLElement.onpointerup: callback EventHandler
  HTMLElement.onprogress: callback EventHandler
  HTMLElement.onratechange: callback EventHandler
  HTMLElement.onreset: callback EventHandler
  HTMLElement.onresize: callback EventHandler
  HTMLElement.onscroll: callback EventHandler
  HTMLElement.onscrollend: callback EventHandler
  HTMLElement.onsecuritypolicyviolation: callback EventHandler
  HTMLElement.onseeked: callback EventHandler
  HTMLElement.onseeking: callback EventHandler
  HTMLElement.onselect: callback EventHandler
  HTMLElement.onselectionchange: callback EventHandler
  HTMLElement.onselectstart: callback EventHandler
  HTMLElement.onslotchange: callback EventHandler
  HTMLElement.onstalled: callback EventHandler
  HTMLElement.onsubmit: callback EventHandler
  HTMLElement.onsuspend: callback EventHandler
  HTMLElement.ontimeupdate: callback EventHandler
  HTMLElement.ontoggle: callback EventHandler
  HTMLElement.ontouchcancel: callback EventHandler
  HTMLElement.ontouchend: callback EventHandler
  HTMLElement.ontouchforcechange: callback EventHandler
  HTMLElement.ontouchmove: callback EventHandler
  HTMLElement.ontouchstart: callback EventHandler
  HTMLElement.ontransitioncancel: callback EventHandler
  HTMLElement.ontransitionend: callback EventHandler
  HTMLElement.ontransitionrun: callback EventHandler
  HTMLElement.ontransitionstart: callback EventHandler
  HTMLElement.onvolumechange: callback EventHandler
  HTMLElement.onwaiting: callback EventHandler
  HTMLElement.onwebkitanimationend: WebKit-prefixed
  HTMLElement.onwebkitanimationiteration: WebKit-prefixed
  HTMLElement.onwebkitanimationstart: WebKit-prefixed
  HTMLElement.onwebkitmouseforcechanged: WebKit-prefixed
  HTMLElement.onwebkitmouseforcedown: WebKit-prefixed
  HTMLElement.onwebkitmouseforceup: WebKit-prefixed
  HTMLElement.onwebkitmouseforcewillbegin: WebKit-prefixed
  HTMLElement.onwebkittransitionend: WebKit-prefixed
  HTMLElement.onwheel: callback EventHandler
  HTMLElement.webkitdropzone: WebKit-prefixed
  HTMLFormElement.requestSubmit: excluded by gen/dom.json
  HTMLFormElement.submit: excluded by gen/dom.json
  HTMLImageElement.decode: excluded by gen/dom.json
  HTMLInputElement.autofillAvailable: [EnabledForWorld]
  HTMLInputElement.autofillButtonType: [EnabledForWorld]
  HTMLInputElement.autofillSpellcheck: [EnabledForWorld]
  HTMLInputElement.autofillVisibility: [EnabledForWorld]
  HTMLInputElement.autofilled: [EnabledForWorld]
  HTMLInputElement.autofilledAndObscured: [EnabledForWorld]
  HTMLInputElement.autofilledAndViewable: [EnabledForWorld]
  HTMLInputElement.files: interface FileList outside gen/dom.json
  HTMLInputElement.lastChangeWasUserEdit: [EnabledForWorld]
  HTMLInputElement.showPicker: excluded by gen/dom.json
  HTMLInputElement.valueAsDate: type Date?
  HTMLOutputElement.constructor: constructor
  HTMLSelectElement.showPicker: excluded by gen/dom.json
  HTMLTextAreaElement.lastChangeWasUserEdit: [EnabledForWorld]
  ImportNodeOptions.customElementRegistry: excluded dictionary field by gen/dom.json
  InputEvent.constructor: constructor
  InputEvent.dataTransfer: interface DataTransfer outside gen/dom.json
  KeyboardEvent.constructor: constructor
  KeyboardEvent.initKeyboardEvent: type WindowProxy?
  MouseEvent.constructor: constructor
  MouseEvent.initMouseEvent: type WindowProxy?
  MouseEvent.webkitForce: WebKit-prefixed
  PointerEvent.constructor: constructor
  Range.constructor: constructor
  Range.getClientRects: interface DOMRectList outside gen/dom.json
  ShadowRoot.adoptedStyleSheets: type any
  ShadowRoot.customElementRegistry: interface CustomElementRegistry outside gen/dom.json
  ShadowRoot.getAnimations: interface WebAnimation outside gen/dom.json
  ShadowRoot.onslotchange: callback EventHandler
  ShadowRoot.styleSheets: interface StyleSheetList outside gen/dom.json
  StaticRange.constructor: constructor
  Text.constructor: constructor
  UIEvent.constructor: constructor
  UIEvent.initUIEvent: type WindowProxy?
  UIEvent.view: type WindowProxy?
  WheelEvent.constructor: constructor
  WheelEvent.webkitDirectionInvertedFromDevice: WebKit-prefixed

Contract-only interfaces (own members have no native exports):
  StylePropertyMapReadOnly: css/typedom/StylePropertyMapReadOnly.idl
    StylePropertyMapReadOnly.get: contract-only interface; member omitted
    StylePropertyMapReadOnly.getAll: contract-only interface; member omitted
    StylePropertyMapReadOnly.has: contract-only interface; member omitted
    StylePropertyMapReadOnly.size: contract-only interface; member omitted
  StyleSheet: css/StyleSheet.idl
    StyleSheet.type: contract-only interface; member omitted
    StyleSheet.href: contract-only interface; member omitted
    StyleSheet.ownerNode: contract-only interface; member omitted
    StyleSheet.parentStyleSheet: contract-only interface; member omitted
    StyleSheet.title: contract-only interface; member omitted
    StyleSheet.media: contract-only interface; member omitted
    StyleSheet.disabled: contract-only interface; member omitted
  SVGTextContentElement: svg/SVGTextContentElement.idl
    SVGTextContentElement.LENGTHADJUST_UNKNOWN: contract-only interface; member omitted
    SVGTextContentElement.LENGTHADJUST_SPACING: contract-only interface; member omitted
    SVGTextContentElement.LENGTHADJUST_SPACINGANDGLYPHS: contract-only interface; member omitted
    SVGTextContentElement.textLength: contract-only interface; member omitted
    SVGTextContentElement.lengthAdjust: contract-only interface; member omitted
    SVGTextContentElement.getNumberOfChars: contract-only interface; member omitted
    SVGTextContentElement.getComputedTextLength: contract-only interface; member omitted
    SVGTextContentElement.getSubStringLength: contract-only interface; member omitted
    SVGTextContentElement.getStartPositionOfChar: contract-only interface; member omitted
    SVGTextContentElement.getEndPositionOfChar: contract-only interface; member omitted
    SVGTextContentElement.getExtentOfChar: contract-only interface; member omitted
    SVGTextContentElement.getRotationOfChar: contract-only interface; member omitted
    SVGTextContentElement.getCharNumAtPosition: contract-only interface; member omitted
    SVGTextContentElement.selectSubString: contract-only interface; member omitted
  SVGTextPositioningElement: svg/SVGTextPositioningElement.idl
    SVGTextPositioningElement.x: contract-only interface; member omitted
    SVGTextPositioningElement.y: contract-only interface; member omitted
    SVGTextPositioningElement.dx: contract-only interface; member omitted
    SVGTextPositioningElement.dy: contract-only interface; member omitted
    SVGTextPositioningElement.rotate: contract-only interface; member omitted
  SVGGradientElement: svg/SVGGradientElement.idl
    SVGGradientElement.SVG_SPREADMETHOD_UNKNOWN: contract-only interface; member omitted
    SVGGradientElement.SVG_SPREADMETHOD_PAD: contract-only interface; member omitted
    SVGGradientElement.SVG_SPREADMETHOD_REFLECT: contract-only interface; member omitted
    SVGGradientElement.SVG_SPREADMETHOD_REPEAT: contract-only interface; member omitted
    SVGGradientElement.gradientUnits: contract-only interface; member omitted
    SVGGradientElement.gradientTransform: contract-only interface; member omitted
    SVGGradientElement.spreadMethod: contract-only interface; member omitted
  SVGComponentTransferFunctionElement: svg/SVGComponentTransferFunctionElement.idl
    SVGComponentTransferFunctionElement.SVG_FECOMPONENTTRANSFER_TYPE_UNKNOWN: contract-only interface; member omitted
    SVGComponentTransferFunctionElement.SVG_FECOMPONENTTRANSFER_TYPE_IDENTITY: contract-only interface; member omitted
    SVGComponentTransferFunctionElement.SVG_FECOMPONENTTRANSFER_TYPE_TABLE: contract-only interface; member omitted
    SVGComponentTransferFunctionElement.SVG_FECOMPONENTTRANSFER_TYPE_DISCRETE: contract-only interface; member omitted
    SVGComponentTransferFunctionElement.SVG_FECOMPONENTTRANSFER_TYPE_LINEAR: contract-only interface; member omitted
    SVGComponentTransferFunctionElement.SVG_FECOMPONENTTRANSFER_TYPE_GAMMA: contract-only interface; member omitted
    SVGComponentTransferFunctionElement.type: contract-only interface; member omitted
    SVGComponentTransferFunctionElement.tableValues: contract-only interface; member omitted
    SVGComponentTransferFunctionElement.slope: contract-only interface; member omitted
    SVGComponentTransferFunctionElement.intercept: contract-only interface; member omitted
    SVGComponentTransferFunctionElement.amplitude: contract-only interface; member omitted
    SVGComponentTransferFunctionElement.exponent: contract-only interface; member omitted
    SVGComponentTransferFunctionElement.offset: contract-only interface; member omitted
  SVGGeometryElement: svg/SVGGeometryElement.idl
    SVGGeometryElement.pathLength: contract-only interface; member omitted
    SVGGeometryElement.isPointInFill: contract-only interface; member omitted
    SVGGeometryElement.isPointInStroke: contract-only interface; member omitted
    SVGGeometryElement.getTotalLength: contract-only interface; member omitted
    SVGGeometryElement.getPointAtLength: contract-only interface; member omitted
  SVGAnimationElement: svg/SVGAnimationElement.idl
    SVGAnimationElement.targetElement: contract-only interface; member omitted
    SVGAnimationElement.getStartTime: contract-only interface; member omitted
    SVGAnimationElement.getCurrentTime: contract-only interface; member omitted
    SVGAnimationElement.getSimpleDuration: contract-only interface; member omitted
    SVGAnimationElement.beginElement: contract-only interface; member omitted
    SVGAnimationElement.beginElementAt: contract-only interface; member omitted
    SVGAnimationElement.endElement: contract-only interface; member omitted
    SVGAnimationElement.endElementAt: contract-only interface; member omitted
    SVGAnimationElement.onbegin: contract-only interface; member omitted
    SVGAnimationElement.onend: contract-only interface; member omitted
    SVGAnimationElement.onrepeat: contract-only interface; member omitted
  SVGGraphicsElement: svg/SVGGraphicsElement.idl
    SVGGraphicsElement.transform: contract-only interface; member omitted
    SVGGraphicsElement.getBBox: contract-only interface; member omitted
    SVGGraphicsElement.getCTM: contract-only interface; member omitted
    SVGGraphicsElement.getScreenCTM: contract-only interface; member omitted
  HTMLMediaElement: html/HTMLMediaElement.idl
    HTMLMediaElement.error: contract-only interface; member omitted
    HTMLMediaElement.src: contract-only interface; member omitted
    HTMLMediaElement.srcObject: contract-only interface; member omitted
    HTMLMediaElement.currentSrc: contract-only interface; member omitted
    HTMLMediaElement.crossOrigin: contract-only interface; member omitted
    HTMLMediaElement.NETWORK_EMPTY: contract-only interface; member omitted
    HTMLMediaElement.NETWORK_IDLE: contract-only interface; member omitted
    HTMLMediaElement.NETWORK_LOADING: contract-only interface; member omitted
    HTMLMediaElement.NETWORK_NO_SOURCE: contract-only interface; member omitted
    HTMLMediaElement.networkState: contract-only interface; member omitted
    HTMLMediaElement.preload: contract-only interface; member omitted
    HTMLMediaElement.buffered: contract-only interface; member omitted
    HTMLMediaElement.load: contract-only interface; member omitted
    HTMLMediaElement.canPlayType: contract-only interface; member omitted
    HTMLMediaElement.HAVE_NOTHING: contract-only interface; member omitted
    HTMLMediaElement.HAVE_METADATA: contract-only interface; member omitted
    HTMLMediaElement.HAVE_CURRENT_DATA: contract-only interface; member omitted
    HTMLMediaElement.HAVE_FUTURE_DATA: contract-only interface; member omitted
    HTMLMediaElement.HAVE_ENOUGH_DATA: contract-only interface; member omitted
    HTMLMediaElement.readyState: contract-only interface; member omitted
    HTMLMediaElement.seeking: contract-only interface; member omitted
    HTMLMediaElement.currentTime: contract-only interface; member omitted
    HTMLMediaElement.fastSeek: contract-only interface; member omitted
    HTMLMediaElement.duration: contract-only interface; member omitted
    HTMLMediaElement.getStartDate: contract-only interface; member omitted
    HTMLMediaElement.paused: contract-only interface; member omitted
    HTMLMediaElement.defaultPlaybackRate: contract-only interface; member omitted
    HTMLMediaElement.playbackRate: contract-only interface; member omitted
    HTMLMediaElement.preservesPitch: contract-only interface; member omitted
    HTMLMediaElement.played: contract-only interface; member omitted
    HTMLMediaElement.seekable: contract-only interface; member omitted
    HTMLMediaElement.ended: contract-only interface; member omitted
    HTMLMediaElement.autoplay: contract-only interface; member omitted
    HTMLMediaElement.loop: contract-only interface; member omitted
    HTMLMediaElement.play: contract-only interface; member omitted
    HTMLMediaElement.pause: contract-only interface; member omitted
    HTMLMediaElement.controls: contract-only interface; member omitted
    HTMLMediaElement.volume: contract-only interface; member omitted
    HTMLMediaElement.muted: contract-only interface; member omitted
    HTMLMediaElement.defaultMuted: contract-only interface; member omitted
    HTMLMediaElement.webkitAudioDecodedByteCount: contract-only interface; member omitted
    HTMLMediaElement.webkitVideoDecodedByteCount: contract-only interface; member omitted
    HTMLMediaElement.webkitKeys: contract-only interface; member omitted
    HTMLMediaElement.webkitSetMediaKeys: contract-only interface; member omitted
    HTMLMediaElement.mediaKeys: contract-only interface; member omitted
    HTMLMediaElement.onencrypted: contract-only interface; member omitted
    HTMLMediaElement.onwaitingforkey: contract-only interface; member omitted
    HTMLMediaElement.setMediaKeys: contract-only interface; member omitted
    HTMLMediaElement.addTextTrack: contract-only interface; member omitted
    HTMLMediaElement.audioTracks: contract-only interface; member omitted
    HTMLMediaElement.textTracks: contract-only interface; member omitted
    HTMLMediaElement.videoTracks: contract-only interface; member omitted
    HTMLMediaElement.mediaGroup: contract-only interface; member omitted
    HTMLMediaElement.controller: contract-only interface; member omitted
    HTMLMediaElement.getVideoPlaybackQuality: contract-only interface; member omitted
    HTMLMediaElement.webkitShowPlaybackTargetPicker: contract-only interface; member omitted
    HTMLMediaElement.webkitCurrentPlaybackTargetIsWireless: contract-only interface; member omitted
    HTMLMediaElement.controlsHost: contract-only interface; member omitted
  HTMLUnknownElement: html/HTMLUnknownElement.idl
  HTMLAreaElement: html/HTMLAreaElement.idl
    HTMLAreaElement.alt: contract-only interface; member omitted
    HTMLAreaElement.coords: contract-only interface; member omitted
    HTMLAreaElement.noHref: contract-only interface; member omitted
    HTMLAreaElement.ping: contract-only interface; member omitted
    HTMLAreaElement.rel: contract-only interface; member omitted
    HTMLAreaElement.shape: contract-only interface; member omitted
    HTMLAreaElement.download: contract-only interface; member omitted
    HTMLAreaElement.referrerPolicy: contract-only interface; member omitted
    HTMLAreaElement.relList: contract-only interface; member omitted
  HTMLAttachmentElement: html/HTMLAttachmentElement.idl
    HTMLAttachmentElement.file: contract-only interface; member omitted
    HTMLAttachmentElement.uniqueIdentifier: contract-only interface; member omitted
    HTMLAttachmentElement.saveButtonClientRect: contract-only interface; member omitted
    HTMLAttachmentElement.getAttachmentIdentifier: contract-only interface; member omitted
  HTMLAudioElement: html/HTMLAudioElement.idl
  HTMLBaseElement: html/HTMLBaseElement.idl
    HTMLBaseElement.href: contract-only interface; member omitted
    HTMLBaseElement.target: contract-only interface; member omitted
  HTMLQuoteElement: html/HTMLQuoteElement.idl
    HTMLQuoteElement.cite: contract-only interface; member omitted
  HTMLBRElement: html/HTMLBRElement.idl
    HTMLBRElement.clear: contract-only interface; member omitted
  HTMLCanvasElement: html/HTMLCanvasElement.idl
    HTMLCanvasElement.width: contract-only interface; member omitted
    HTMLCanvasElement.height: contract-only interface; member omitted
    HTMLCanvasElement.getContext: contract-only interface; member omitted
    HTMLCanvasElement.toDataURL: contract-only interface; member omitted
    HTMLCanvasElement.toBlob: contract-only interface; member omitted
    HTMLCanvasElement.transferControlToOffscreen: contract-only interface; member omitted
    HTMLCanvasElement.captureStream: contract-only interface; member omitted
  HTMLTableCaptionElement: html/HTMLTableCaptionElement.idl
    HTMLTableCaptionElement.align: contract-only interface; member omitted
  HTMLTableColElement: html/HTMLTableColElement.idl
    HTMLTableColElement.align: contract-only interface; member omitted
    HTMLTableColElement.ch: contract-only interface; member omitted
    HTMLTableColElement.chOff: contract-only interface; member omitted
    HTMLTableColElement.span: contract-only interface; member omitted
    HTMLTableColElement.vAlign: contract-only interface; member omitted
    HTMLTableColElement.width: contract-only interface; member omitted
  HTMLDataElement: html/HTMLDataElement.idl
    HTMLDataElement.value: contract-only interface; member omitted
  HTMLDataListElement: html/HTMLDataListElement.idl
    HTMLDataListElement.options: contract-only interface; member omitted
  HTMLModElement: html/HTMLModElement.idl
    HTMLModElement.cite: contract-only interface; member omitted
    HTMLModElement.dateTime: contract-only interface; member omitted
  HTMLDirectoryElement: html/HTMLDirectoryElement.idl
    HTMLDirectoryElement.compact: contract-only interface; member omitted
  HTMLDListElement: html/HTMLDListElement.idl
    HTMLDListElement.compact: contract-only interface; member omitted
  HTMLEmbedElement: html/HTMLEmbedElement.idl
    HTMLEmbedElement.align: contract-only interface; member omitted
    HTMLEmbedElement.height: contract-only interface; member omitted
    HTMLEmbedElement.name: contract-only interface; member omitted
    HTMLEmbedElement.src: contract-only interface; member omitted
    HTMLEmbedElement.type: contract-only interface; member omitted
    HTMLEmbedElement.width: contract-only interface; member omitted
    HTMLEmbedElement.getSVGDocument: contract-only interface; member omitted
  HTMLFontElement: html/HTMLFontElement.idl
    HTMLFontElement.color: contract-only interface; member omitted
    HTMLFontElement.face: contract-only interface; member omitted
    HTMLFontElement.size: contract-only interface; member omitted
  HTMLFrameElement: html/HTMLFrameElement.idl
    HTMLFrameElement.name: contract-only interface; member omitted
    HTMLFrameElement.scrolling: contract-only interface; member omitted
    HTMLFrameElement.src: contract-only interface; member omitted
    HTMLFrameElement.frameBorder: contract-only interface; member omitted
    HTMLFrameElement.longDesc: contract-only interface; member omitted
    HTMLFrameElement.noResize: contract-only interface; member omitted
    HTMLFrameElement.contentDocument: contract-only interface; member omitted
    HTMLFrameElement.contentWindow: contract-only interface; member omitted
    HTMLFrameElement.marginHeight: contract-only interface; member omitted
    HTMLFrameElement.marginWidth: contract-only interface; member omitted
  HTMLFrameSetElement: html/HTMLFrameSetElement.idl
    HTMLFrameSetElement.cols: contract-only interface; member omitted
    HTMLFrameSetElement.rows: contract-only interface; member omitted
    HTMLFrameSetElement.onblur: contract-only interface; member omitted
    HTMLFrameSetElement.onerror: contract-only interface; member omitted
    HTMLFrameSetElement.onfocus: contract-only interface; member omitted
    HTMLFrameSetElement.onload: contract-only interface; member omitted
    HTMLFrameSetElement.onresize: contract-only interface; member omitted
    HTMLFrameSetElement.onscroll: contract-only interface; member omitted
    HTMLFrameSetElement.onfocusin: contract-only interface; member omitted
    HTMLFrameSetElement.onfocusout: contract-only interface; member omitted
    HTMLFrameSetElement.getter: contract-only interface; member omitted
  HTMLHeadingElement: html/HTMLHeadingElement.idl
    HTMLHeadingElement.align: contract-only interface; member omitted
  HTMLHRElement: html/HTMLHRElement.idl
    HTMLHRElement.align: contract-only interface; member omitted
    HTMLHRElement.color: contract-only interface; member omitted
    HTMLHRElement.noShade: contract-only interface; member omitted
    HTMLHRElement.size: contract-only interface; member omitted
    HTMLHRElement.width: contract-only interface; member omitted
  HTMLHtmlElement: html/HTMLHtmlElement.idl
    HTMLHtmlElement.version: contract-only interface; member omitted
  HTMLIFrameElement: html/HTMLIFrameElement.idl
    HTMLIFrameElement.src: contract-only interface; member omitted
    HTMLIFrameElement.srcdoc: contract-only interface; member omitted
    HTMLIFrameElement.name: contract-only interface; member omitted
    HTMLIFrameElement.sandbox: contract-only interface; member omitted
    HTMLIFrameElement.allow: contract-only interface; member omitted
    HTMLIFrameElement.allowFullscreen: contract-only interface; member omitted
    HTMLIFrameElement.width: contract-only interface; member omitted
    HTMLIFrameElement.height: contract-only interface; member omitted
    HTMLIFrameElement.referrerPolicy: contract-only interface; member omitted
    HTMLIFrameElement.loading: contract-only interface; member omitted
    HTMLIFrameElement.contentDocument: contract-only interface; member omitted
    HTMLIFrameElement.contentWindow: contract-only interface; member omitted
    HTMLIFrameElement.getSVGDocument: contract-only interface; member omitted
    HTMLIFrameElement.align: contract-only interface; member omitted
    HTMLIFrameElement.scrolling: contract-only interface; member omitted
    HTMLIFrameElement.frameBorder: contract-only interface; member omitted
    HTMLIFrameElement.longDesc: contract-only interface; member omitted
    HTMLIFrameElement.marginHeight: contract-only interface; member omitted
    HTMLIFrameElement.marginWidth: contract-only interface; member omitted
  HTMLLinkElement: html/HTMLLinkElement.idl
    HTMLLinkElement.href: contract-only interface; member omitted
    HTMLLinkElement.crossOrigin: contract-only interface; member omitted
    HTMLLinkElement.rel: contract-only interface; member omitted
    HTMLLinkElement.as: contract-only interface; member omitted
    HTMLLinkElement.relList: contract-only interface; member omitted
    HTMLLinkElement.media: contract-only interface; member omitted
    HTMLLinkElement.integrity: contract-only interface; member omitted
    HTMLLinkElement.hreflang: contract-only interface; member omitted
    HTMLLinkElement.type: contract-only interface; member omitted
    HTMLLinkElement.sizes: contract-only interface; member omitted
    HTMLLinkElement.imageSrcset: contract-only interface; member omitted
    HTMLLinkElement.imageSizes: contract-only interface; member omitted
    HTMLLinkElement.referrerPolicy: contract-only interface; member omitted
    HTMLLinkElement.blocking: contract-only interface; member omitted
    HTMLLinkElement.disabled: contract-only interface; member omitted
    HTMLLinkElement.fetchPriority: contract-only interface; member omitted
    HTMLLinkElement.charset: contract-only interface; member omitted
    HTMLLinkElement.rev: contract-only interface; member omitted
    HTMLLinkElement.target: contract-only interface; member omitted
  HTMLPreElement: html/HTMLPreElement.idl
    HTMLPreElement.width: contract-only interface; member omitted
  HTMLMapElement: html/HTMLMapElement.idl
    HTMLMapElement.areas: contract-only interface; member omitted
    HTMLMapElement.name: contract-only interface; member omitted
  HTMLMarqueeElement: html/HTMLMarqueeElement.idl
    HTMLMarqueeElement.start: contract-only interface; member omitted
    HTMLMarqueeElement.stop: contract-only interface; member omitted
    HTMLMarqueeElement.behavior: contract-only interface; member omitted
    HTMLMarqueeElement.bgColor: contract-only interface; member omitted
    HTMLMarqueeElement.direction: contract-only interface; member omitted
    HTMLMarqueeElement.height: contract-only interface; member omitted
    HTMLMarqueeElement.hspace: contract-only interface; member omitted
    HTMLMarqueeElement.loop: contract-only interface; member omitted
    HTMLMarqueeElement.scrollAmount: contract-only interface; member omitted
    HTMLMarqueeElement.scrollDelay: contract-only interface; member omitted
    HTMLMarqueeElement.trueSpeed: contract-only interface; member omitted
    HTMLMarqueeElement.vspace: contract-only interface; member omitted
    HTMLMarqueeElement.width: contract-only interface; member omitted
  HTMLMenuElement: html/HTMLMenuElement.idl
    HTMLMenuElement.compact: contract-only interface; member omitted
  HTMLMetaElement: html/HTMLMetaElement.idl
    HTMLMetaElement.content: contract-only interface; member omitted
    HTMLMetaElement.httpEquiv: contract-only interface; member omitted
    HTMLMetaElement.media: contract-only interface; member omitted
    HTMLMetaElement.name: contract-only interface; member omitted
    HTMLMetaElement.scheme: contract-only interface; member omitted
  HTMLModelElement: Modules/model-element/HTMLModelElement.idl
    HTMLModelElement.width: contract-only interface; member omitted
    HTMLModelElement.height: contract-only interface; member omitted
    HTMLModelElement.src: contract-only interface; member omitted
    HTMLModelElement.currentSrc: contract-only interface; member omitted
    HTMLModelElement.complete: contract-only interface; member omitted
    HTMLModelElement.ready: contract-only interface; member omitted
    HTMLModelElement.interactive: contract-only interface; member omitted
    HTMLModelElement.boundingBoxCenter: contract-only interface; member omitted
    HTMLModelElement.boundingBoxExtents: contract-only interface; member omitted
    HTMLModelElement.entityTransform: contract-only interface; member omitted
    HTMLModelElement.autoplay: contract-only interface; member omitted
    HTMLModelElement.loop: contract-only interface; member omitted
    HTMLModelElement.playbackRate: contract-only interface; member omitted
    HTMLModelElement.duration: contract-only interface; member omitted
    HTMLModelElement.paused: contract-only interface; member omitted
    HTMLModelElement.play: contract-only interface; member omitted
    HTMLModelElement.pause: contract-only interface; member omitted
    HTMLModelElement.currentTime: contract-only interface; member omitted
    HTMLModelElement.stageMode: contract-only interface; member omitted
    HTMLModelElement.environmentMap: contract-only interface; member omitted
    HTMLModelElement.environmentMapReady: contract-only interface; member omitted
    HTMLModelElement.noportal: contract-only interface; member omitted
    HTMLModelElement.enterFullscreen: contract-only interface; member omitted
    HTMLModelElement.onimmersivechange: contract-only interface; member omitted
    HTMLModelElement.onimmersiveerror: contract-only interface; member omitted
    HTMLModelElement.requestImmersive: contract-only interface; member omitted
  HTMLObjectElement: html/HTMLObjectElement.idl
    HTMLObjectElement.data: contract-only interface; member omitted
    HTMLObjectElement.type: contract-only interface; member omitted
    HTMLObjectElement.name: contract-only interface; member omitted
    HTMLObjectElement.form: contract-only interface; member omitted
    HTMLObjectElement.width: contract-only interface; member omitted
    HTMLObjectElement.height: contract-only interface; member omitted
    HTMLObjectElement.contentDocument: contract-only interface; member omitted
    HTMLObjectElement.contentWindow: contract-only interface; member omitted
    HTMLObjectElement.getSVGDocument: contract-only interface; member omitted
    HTMLObjectElement.willValidate: contract-only interface; member omitted
    HTMLObjectElement.validity: contract-only interface; member omitted
    HTMLObjectElement.validationMessage: contract-only interface; member omitted
    HTMLObjectElement.checkValidity: contract-only interface; member omitted
    HTMLObjectElement.reportValidity: contract-only interface; member omitted
    HTMLObjectElement.setCustomValidity: contract-only interface; member omitted
    HTMLObjectElement.align: contract-only interface; member omitted
    HTMLObjectElement.archive: contract-only interface; member omitted
    HTMLObjectElement.code: contract-only interface; member omitted
    HTMLObjectElement.declare: contract-only interface; member omitted
    HTMLObjectElement.hspace: contract-only interface; member omitted
    HTMLObjectElement.standby: contract-only interface; member omitted
    HTMLObjectElement.vspace: contract-only interface; member omitted
    HTMLObjectElement.codeBase: contract-only interface; member omitted
    HTMLObjectElement.codeType: contract-only interface; member omitted
    HTMLObjectElement.useMap: contract-only interface; member omitted
    HTMLObjectElement.border: contract-only interface; member omitted
  HTMLParamElement: html/HTMLParamElement.idl
    HTMLParamElement.name: contract-only interface; member omitted
    HTMLParamElement.type: contract-only interface; member omitted
    HTMLParamElement.value: contract-only interface; member omitted
    HTMLParamElement.valueType: contract-only interface; member omitted
  HTMLPictureElement: html/HTMLPictureElement.idl
  HTMLScriptElement: html/HTMLScriptElement.idl
    HTMLScriptElement.text: contract-only interface; member omitted
    HTMLScriptElement.innerText: contract-only interface; member omitted
    HTMLScriptElement.textContent: contract-only interface; member omitted
    HTMLScriptElement.htmlFor: contract-only interface; member omitted
    HTMLScriptElement.event: contract-only interface; member omitted
    HTMLScriptElement.charset: contract-only interface; member omitted
    HTMLScriptElement.async: contract-only interface; member omitted
    HTMLScriptElement.defer: contract-only interface; member omitted
    HTMLScriptElement.src: contract-only interface; member omitted
    HTMLScriptElement.type: contract-only interface; member omitted
    HTMLScriptElement.crossOrigin: contract-only interface; member omitted
    HTMLScriptElement.noModule: contract-only interface; member omitted
    HTMLScriptElement.integrity: contract-only interface; member omitted
    HTMLScriptElement.referrerPolicy: contract-only interface; member omitted
    HTMLScriptElement.fetchPriority: contract-only interface; member omitted
    HTMLScriptElement.blocking: contract-only interface; member omitted
    HTMLScriptElement.supports: contract-only interface; member omitted
  HTMLSelectedContentElement: html/HTMLSelectedContentElement.idl
  HTMLSourceElement: html/HTMLSourceElement.idl
    HTMLSourceElement.src: contract-only interface; member omitted
    HTMLSourceElement.type: contract-only interface; member omitted
    HTMLSourceElement.srcset: contract-only interface; member omitted
    HTMLSourceElement.sizes: contract-only interface; member omitted
    HTMLSourceElement.media: contract-only interface; member omitted
    HTMLSourceElement.width: contract-only interface; member omitted
    HTMLSourceElement.height: contract-only interface; member omitted
    HTMLSourceElement.attachmentIdentifier: contract-only interface; member omitted
  HTMLStyleElement: html/HTMLStyleElement.idl
    HTMLStyleElement.disabled: contract-only interface; member omitted
    HTMLStyleElement.media: contract-only interface; member omitted
    HTMLStyleElement.type: contract-only interface; member omitted
    HTMLStyleElement.blocking: contract-only interface; member omitted
  HTMLTableElement: html/HTMLTableElement.idl
    HTMLTableElement.caption: contract-only interface; member omitted
    HTMLTableElement.createCaption: contract-only interface; member omitted
    HTMLTableElement.deleteCaption: contract-only interface; member omitted
    HTMLTableElement.tHead: contract-only interface; member omitted
    HTMLTableElement.createTHead: contract-only interface; member omitted
    HTMLTableElement.deleteTHead: contract-only interface; member omitted
    HTMLTableElement.tFoot: contract-only interface; member omitted
    HTMLTableElement.createTFoot: contract-only interface; member omitted
    HTMLTableElement.deleteTFoot: contract-only interface; member omitted
    HTMLTableElement.tBodies: contract-only interface; member omitted
    HTMLTableElement.createTBody: contract-only interface; member omitted
    HTMLTableElement.rows: contract-only interface; member omitted
    HTMLTableElement.insertRow: contract-only interface; member omitted
    HTMLTableElement.deleteRow: contract-only interface; member omitted
    HTMLTableElement.align: contract-only interface; member omitted
    HTMLTableElement.border: contract-only interface; member omitted
    HTMLTableElement.frame: contract-only interface; member omitted
    HTMLTableElement.rules: contract-only interface; member omitted
    HTMLTableElement.summary: contract-only interface; member omitted
    HTMLTableElement.width: contract-only interface; member omitted
    HTMLTableElement.bgColor: contract-only interface; member omitted
    HTMLTableElement.cellPadding: contract-only interface; member omitted
    HTMLTableElement.cellSpacing: contract-only interface; member omitted
  HTMLTableSectionElement: html/HTMLTableSectionElement.idl
    HTMLTableSectionElement.rows: contract-only interface; member omitted
    HTMLTableSectionElement.insertRow: contract-only interface; member omitted
    HTMLTableSectionElement.deleteRow: contract-only interface; member omitted
    HTMLTableSectionElement.align: contract-only interface; member omitted
    HTMLTableSectionElement.ch: contract-only interface; member omitted
    HTMLTableSectionElement.chOff: contract-only interface; member omitted
    HTMLTableSectionElement.vAlign: contract-only interface; member omitted
  HTMLTableCellElement: html/HTMLTableCellElement.idl
    HTMLTableCellElement.cellIndex: contract-only interface; member omitted
    HTMLTableCellElement.align: contract-only interface; member omitted
    HTMLTableCellElement.axis: contract-only interface; member omitted
    HTMLTableCellElement.bgColor: contract-only interface; member omitted
    HTMLTableCellElement.ch: contract-only interface; member omitted
    HTMLTableCellElement.chOff: contract-only interface; member omitted
    HTMLTableCellElement.colSpan: contract-only interface; member omitted
    HTMLTableCellElement.rowSpan: contract-only interface; member omitted
    HTMLTableCellElement.headers: contract-only interface; member omitted
    HTMLTableCellElement.height: contract-only interface; member omitted
    HTMLTableCellElement.noWrap: contract-only interface; member omitted
    HTMLTableCellElement.vAlign: contract-only interface; member omitted
    HTMLTableCellElement.width: contract-only interface; member omitted
    HTMLTableCellElement.abbr: contract-only interface; member omitted
    HTMLTableCellElement.scope: contract-only interface; member omitted
  HTMLTimeElement: html/HTMLTimeElement.idl
    HTMLTimeElement.dateTime: contract-only interface; member omitted
  HTMLTitleElement: html/HTMLTitleElement.idl
    HTMLTitleElement.text: contract-only interface; member omitted
  HTMLTableRowElement: html/HTMLTableRowElement.idl
    HTMLTableRowElement.rowIndex: contract-only interface; member omitted
    HTMLTableRowElement.sectionRowIndex: contract-only interface; member omitted
    HTMLTableRowElement.cells: contract-only interface; member omitted
    HTMLTableRowElement.align: contract-only interface; member omitted
    HTMLTableRowElement.bgColor: contract-only interface; member omitted
    HTMLTableRowElement.ch: contract-only interface; member omitted
    HTMLTableRowElement.chOff: contract-only interface; member omitted
    HTMLTableRowElement.vAlign: contract-only interface; member omitted
    HTMLTableRowElement.insertCell: contract-only interface; member omitted
    HTMLTableRowElement.deleteCell: contract-only interface; member omitted
  HTMLTrackElement: html/HTMLTrackElement.idl
    HTMLTrackElement.kind: contract-only interface; member omitted
    HTMLTrackElement.src: contract-only interface; member omitted
    HTMLTrackElement.srclang: contract-only interface; member omitted
    HTMLTrackElement.label: contract-only interface; member omitted
    HTMLTrackElement.default: contract-only interface; member omitted
    HTMLTrackElement.NONE: contract-only interface; member omitted
    HTMLTrackElement.LOADING: contract-only interface; member omitted
    HTMLTrackElement.LOADED: contract-only interface; member omitted
    HTMLTrackElement.ERROR: contract-only interface; member omitted
    HTMLTrackElement.readyState: contract-only interface; member omitted
    HTMLTrackElement.track: contract-only interface; member omitted
  HTMLVideoElement: html/HTMLVideoElement.idl
    HTMLVideoElement.width: contract-only interface; member omitted
    HTMLVideoElement.height: contract-only interface; member omitted
    HTMLVideoElement.videoWidth: contract-only interface; member omitted
    HTMLVideoElement.videoHeight: contract-only interface; member omitted
    HTMLVideoElement.poster: contract-only interface; member omitted
    HTMLVideoElement.playsInline: contract-only interface; member omitted
    HTMLVideoElement.webkitSupportsFullscreen: contract-only interface; member omitted
    HTMLVideoElement.webkitDisplayingFullscreen: contract-only interface; member omitted
    HTMLVideoElement.webkitEnterFullscreen: contract-only interface; member omitted
    HTMLVideoElement.webkitExitFullscreen: contract-only interface; member omitted
    HTMLVideoElement.webkitEnterFullScreen: contract-only interface; member omitted
    HTMLVideoElement.webkitExitFullScreen: contract-only interface; member omitted
    HTMLVideoElement.webkitWirelessVideoPlaybackDisabled: contract-only interface; member omitted
    HTMLVideoElement.webkitDecodedFrameCount: contract-only interface; member omitted
    HTMLVideoElement.webkitDroppedFrameCount: contract-only interface; member omitted
    HTMLVideoElement.getVideoPlaybackQuality: contract-only interface; member omitted
    HTMLVideoElement.webkitSupportsPresentationMode: contract-only interface; member omitted
    HTMLVideoElement.webkitPresentationMode: contract-only interface; member omitted
    HTMLVideoElement.webkitSetPresentationMode: contract-only interface; member omitted
  SVGAElement: svg/SVGAElement.idl
    SVGAElement.target: contract-only interface; member omitted
    SVGAElement.hreflang: contract-only interface; member omitted
    SVGAElement.rel: contract-only interface; member omitted
    SVGAElement.relList: contract-only interface; member omitted
    SVGAElement.type: contract-only interface; member omitted
  SVGElement: svg/SVGElement.idl
    SVGElement.className: contract-only interface; member omitted
    SVGElement.ownerSVGElement: contract-only interface; member omitted
    SVGElement.viewportElement: contract-only interface; member omitted
  SVGAnimateElement: svg/SVGAnimateElement.idl
  SVGAnimateMotionElement: svg/SVGAnimateMotionElement.idl
  SVGAnimateTransformElement: svg/SVGAnimateTransformElement.idl
  SVGSetElement: svg/SVGSetElement.idl
  SVGCircleElement: svg/SVGCircleElement.idl
    SVGCircleElement.cx: contract-only interface; member omitted
    SVGCircleElement.cy: contract-only interface; member omitted
    SVGCircleElement.r: contract-only interface; member omitted
  SVGClipPathElement: svg/SVGClipPathElement.idl
    SVGClipPathElement.clipPathUnits: contract-only interface; member omitted
    SVGClipPathElement.transform: contract-only interface; member omitted
  SVGDefsElement: svg/SVGDefsElement.idl
  SVGDescElement: svg/SVGDescElement.idl
  SVGEllipseElement: svg/SVGEllipseElement.idl
    SVGEllipseElement.cx: contract-only interface; member omitted
    SVGEllipseElement.cy: contract-only interface; member omitted
    SVGEllipseElement.rx: contract-only interface; member omitted
    SVGEllipseElement.ry: contract-only interface; member omitted
  SVGFEBlendElement: svg/SVGFEBlendElement.idl
    SVGFEBlendElement.SVG_FEBLEND_MODE_UNKNOWN: contract-only interface; member omitted
    SVGFEBlendElement.SVG_FEBLEND_MODE_NORMAL: contract-only interface; member omitted
    SVGFEBlendElement.SVG_FEBLEND_MODE_MULTIPLY: contract-only interface; member omitted
    SVGFEBlendElement.SVG_FEBLEND_MODE_SCREEN: contract-only interface; member omitted
    SVGFEBlendElement.SVG_FEBLEND_MODE_DARKEN: contract-only interface; member omitted
    SVGFEBlendElement.SVG_FEBLEND_MODE_LIGHTEN: contract-only interface; member omitted
    SVGFEBlendElement.SVG_FEBLEND_MODE_OVERLAY: contract-only interface; member omitted
    SVGFEBlendElement.SVG_FEBLEND_MODE_COLOR_DODGE: contract-only interface; member omitted
    SVGFEBlendElement.SVG_FEBLEND_MODE_COLOR_BURN: contract-only interface; member omitted
    SVGFEBlendElement.SVG_FEBLEND_MODE_HARD_LIGHT: contract-only interface; member omitted
    SVGFEBlendElement.SVG_FEBLEND_MODE_SOFT_LIGHT: contract-only interface; member omitted
    SVGFEBlendElement.SVG_FEBLEND_MODE_DIFFERENCE: contract-only interface; member omitted
    SVGFEBlendElement.SVG_FEBLEND_MODE_EXCLUSION: contract-only interface; member omitted
    SVGFEBlendElement.SVG_FEBLEND_MODE_HUE: contract-only interface; member omitted
    SVGFEBlendElement.SVG_FEBLEND_MODE_SATURATION: contract-only interface; member omitted
    SVGFEBlendElement.SVG_FEBLEND_MODE_COLOR: contract-only interface; member omitted
    SVGFEBlendElement.SVG_FEBLEND_MODE_LUMINOSITY: contract-only interface; member omitted
    SVGFEBlendElement.in1: contract-only interface; member omitted
    SVGFEBlendElement.in2: contract-only interface; member omitted
    SVGFEBlendElement.mode: contract-only interface; member omitted
  SVGFEColorMatrixElement: svg/SVGFEColorMatrixElement.idl
    SVGFEColorMatrixElement.SVG_FECOLORMATRIX_TYPE_UNKNOWN: contract-only interface; member omitted
    SVGFEColorMatrixElement.SVG_FECOLORMATRIX_TYPE_MATRIX: contract-only interface; member omitted
    SVGFEColorMatrixElement.SVG_FECOLORMATRIX_TYPE_SATURATE: contract-only interface; member omitted
    SVGFEColorMatrixElement.SVG_FECOLORMATRIX_TYPE_HUEROTATE: contract-only interface; member omitted
    SVGFEColorMatrixElement.SVG_FECOLORMATRIX_TYPE_LUMINANCETOALPHA: contract-only interface; member omitted
    SVGFEColorMatrixElement.in1: contract-only interface; member omitted
    SVGFEColorMatrixElement.type: contract-only interface; member omitted
    SVGFEColorMatrixElement.values: contract-only interface; member omitted
  SVGFEComponentTransferElement: svg/SVGFEComponentTransferElement.idl
    SVGFEComponentTransferElement.in1: contract-only interface; member omitted
  SVGFECompositeElement: svg/SVGFECompositeElement.idl
    SVGFECompositeElement.SVG_FECOMPOSITE_OPERATOR_UNKNOWN: contract-only interface; member omitted
    SVGFECompositeElement.SVG_FECOMPOSITE_OPERATOR_OVER: contract-only interface; member omitted
    SVGFECompositeElement.SVG_FECOMPOSITE_OPERATOR_IN: contract-only interface; member omitted
    SVGFECompositeElement.SVG_FECOMPOSITE_OPERATOR_OUT: contract-only interface; member omitted
    SVGFECompositeElement.SVG_FECOMPOSITE_OPERATOR_ATOP: contract-only interface; member omitted
    SVGFECompositeElement.SVG_FECOMPOSITE_OPERATOR_XOR: contract-only interface; member omitted
    SVGFECompositeElement.SVG_FECOMPOSITE_OPERATOR_ARITHMETIC: contract-only interface; member omitted
    SVGFECompositeElement.SVG_FECOMPOSITE_OPERATOR_LIGHTER: contract-only interface; member omitted
    SVGFECompositeElement.in1: contract-only interface; member omitted
    SVGFECompositeElement.in2: contract-only interface; member omitted
    SVGFECompositeElement.operator: contract-only interface; member omitted
    SVGFECompositeElement.k1: contract-only interface; member omitted
    SVGFECompositeElement.k2: contract-only interface; member omitted
    SVGFECompositeElement.k3: contract-only interface; member omitted
    SVGFECompositeElement.k4: contract-only interface; member omitted
  SVGFEConvolveMatrixElement: svg/SVGFEConvolveMatrixElement.idl
    SVGFEConvolveMatrixElement.SVG_EDGEMODE_UNKNOWN: contract-only interface; member omitted
    SVGFEConvolveMatrixElement.SVG_EDGEMODE_DUPLICATE: contract-only interface; member omitted
    SVGFEConvolveMatrixElement.SVG_EDGEMODE_WRAP: contract-only interface; member omitted
    SVGFEConvolveMatrixElement.SVG_EDGEMODE_NONE: contract-only interface; member omitted
    SVGFEConvolveMatrixElement.in1: contract-only interface; member omitted
    SVGFEConvolveMatrixElement.orderX: contract-only interface; member omitted
    SVGFEConvolveMatrixElement.orderY: contract-only interface; member omitted
    SVGFEConvolveMatrixElement.kernelMatrix: contract-only interface; member omitted
    SVGFEConvolveMatrixElement.divisor: contract-only interface; member omitted
    SVGFEConvolveMatrixElement.bias: contract-only interface; member omitted
    SVGFEConvolveMatrixElement.targetX: contract-only interface; member omitted
    SVGFEConvolveMatrixElement.targetY: contract-only interface; member omitted
    SVGFEConvolveMatrixElement.edgeMode: contract-only interface; member omitted
    SVGFEConvolveMatrixElement.kernelUnitLengthX: contract-only interface; member omitted
    SVGFEConvolveMatrixElement.kernelUnitLengthY: contract-only interface; member omitted
    SVGFEConvolveMatrixElement.preserveAlpha: contract-only interface; member omitted
  SVGFEDiffuseLightingElement: svg/SVGFEDiffuseLightingElement.idl
    SVGFEDiffuseLightingElement.in1: contract-only interface; member omitted
    SVGFEDiffuseLightingElement.surfaceScale: contract-only interface; member omitted
    SVGFEDiffuseLightingElement.diffuseConstant: contract-only interface; member omitted
    SVGFEDiffuseLightingElement.kernelUnitLengthX: contract-only interface; member omitted
    SVGFEDiffuseLightingElement.kernelUnitLengthY: contract-only interface; member omitted
  SVGFEDisplacementMapElement: svg/SVGFEDisplacementMapElement.idl
    SVGFEDisplacementMapElement.SVG_CHANNEL_UNKNOWN: contract-only interface; member omitted
    SVGFEDisplacementMapElement.SVG_CHANNEL_R: contract-only interface; member omitted
    SVGFEDisplacementMapElement.SVG_CHANNEL_G: contract-only interface; member omitted
    SVGFEDisplacementMapElement.SVG_CHANNEL_B: contract-only interface; member omitted
    SVGFEDisplacementMapElement.SVG_CHANNEL_A: contract-only interface; member omitted
    SVGFEDisplacementMapElement.in1: contract-only interface; member omitted
    SVGFEDisplacementMapElement.in2: contract-only interface; member omitted
    SVGFEDisplacementMapElement.scale: contract-only interface; member omitted
    SVGFEDisplacementMapElement.xChannelSelector: contract-only interface; member omitted
    SVGFEDisplacementMapElement.yChannelSelector: contract-only interface; member omitted
  SVGFEDistantLightElement: svg/SVGFEDistantLightElement.idl
    SVGFEDistantLightElement.azimuth: contract-only interface; member omitted
    SVGFEDistantLightElement.elevation: contract-only interface; member omitted
  SVGFEDropShadowElement: svg/SVGFEDropShadowElement.idl
    SVGFEDropShadowElement.in1: contract-only interface; member omitted
    SVGFEDropShadowElement.dx: contract-only interface; member omitted
    SVGFEDropShadowElement.dy: contract-only interface; member omitted
    SVGFEDropShadowElement.stdDeviationX: contract-only interface; member omitted
    SVGFEDropShadowElement.stdDeviationY: contract-only interface; member omitted
    SVGFEDropShadowElement.setStdDeviation: contract-only interface; member omitted
  SVGFEFloodElement: svg/SVGFEFloodElement.idl
  SVGFEFuncAElement: svg/SVGFEFuncAElement.idl
  SVGFEFuncBElement: svg/SVGFEFuncBElement.idl
  SVGFEFuncGElement: svg/SVGFEFuncGElement.idl
  SVGFEFuncRElement: svg/SVGFEFuncRElement.idl
  SVGFEGaussianBlurElement: svg/SVGFEGaussianBlurElement.idl
    SVGFEGaussianBlurElement.SVG_EDGEMODE_UNKNOWN: contract-only interface; member omitted
    SVGFEGaussianBlurElement.SVG_EDGEMODE_DUPLICATE: contract-only interface; member omitted
    SVGFEGaussianBlurElement.SVG_EDGEMODE_WRAP: contract-only interface; member omitted
    SVGFEGaussianBlurElement.SVG_EDGEMODE_NONE: contract-only interface; member omitted
    SVGFEGaussianBlurElement.in1: contract-only interface; member omitted
    SVGFEGaussianBlurElement.stdDeviationX: contract-only interface; member omitted
    SVGFEGaussianBlurElement.stdDeviationY: contract-only interface; member omitted
    SVGFEGaussianBlurElement.edgeMode: contract-only interface; member omitted
    SVGFEGaussianBlurElement.setStdDeviation: contract-only interface; member omitted
  SVGFEImageElement: svg/SVGFEImageElement.idl
    SVGFEImageElement.preserveAspectRatio: contract-only interface; member omitted
  SVGFEMergeElement: svg/SVGFEMergeElement.idl
  SVGFEMergeNodeElement: svg/SVGFEMergeNodeElement.idl
    SVGFEMergeNodeElement.in1: contract-only interface; member omitted
  SVGFEMorphologyElement: svg/SVGFEMorphologyElement.idl
    SVGFEMorphologyElement.SVG_MORPHOLOGY_OPERATOR_UNKNOWN: contract-only interface; member omitted
    SVGFEMorphologyElement.SVG_MORPHOLOGY_OPERATOR_ERODE: contract-only interface; member omitted
    SVGFEMorphologyElement.SVG_MORPHOLOGY_OPERATOR_DILATE: contract-only interface; member omitted
    SVGFEMorphologyElement.in1: contract-only interface; member omitted
    SVGFEMorphologyElement.operator: contract-only interface; member omitted
    SVGFEMorphologyElement.radiusX: contract-only interface; member omitted
    SVGFEMorphologyElement.radiusY: contract-only interface; member omitted
  SVGFEOffsetElement: svg/SVGFEOffsetElement.idl
    SVGFEOffsetElement.in1: contract-only interface; member omitted
    SVGFEOffsetElement.dx: contract-only interface; member omitted
    SVGFEOffsetElement.dy: contract-only interface; member omitted
  SVGFEPointLightElement: svg/SVGFEPointLightElement.idl
    SVGFEPointLightElement.x: contract-only interface; member omitted
    SVGFEPointLightElement.y: contract-only interface; member omitted
    SVGFEPointLightElement.z: contract-only interface; member omitted
  SVGFESpecularLightingElement: svg/SVGFESpecularLightingElement.idl
    SVGFESpecularLightingElement.in1: contract-only interface; member omitted
    SVGFESpecularLightingElement.surfaceScale: contract-only interface; member omitted
    SVGFESpecularLightingElement.specularConstant: contract-only interface; member omitted
    SVGFESpecularLightingElement.specularExponent: contract-only interface; member omitted
    SVGFESpecularLightingElement.kernelUnitLengthX: contract-only interface; member omitted
    SVGFESpecularLightingElement.kernelUnitLengthY: contract-only interface; member omitted
  SVGFESpotLightElement: svg/SVGFESpotLightElement.idl
    SVGFESpotLightElement.x: contract-only interface; member omitted
    SVGFESpotLightElement.y: contract-only interface; member omitted
    SVGFESpotLightElement.z: contract-only interface; member omitted
    SVGFESpotLightElement.pointsAtX: contract-only interface; member omitted
    SVGFESpotLightElement.pointsAtY: contract-only interface; member omitted
    SVGFESpotLightElement.pointsAtZ: contract-only interface; member omitted
    SVGFESpotLightElement.specularExponent: contract-only interface; member omitted
    SVGFESpotLightElement.limitingConeAngle: contract-only interface; member omitted
  SVGFETileElement: svg/SVGFETileElement.idl
    SVGFETileElement.in1: contract-only interface; member omitted
  SVGFETurbulenceElement: svg/SVGFETurbulenceElement.idl
    SVGFETurbulenceElement.SVG_TURBULENCE_TYPE_UNKNOWN: contract-only interface; member omitted
    SVGFETurbulenceElement.SVG_TURBULENCE_TYPE_FRACTALNOISE: contract-only interface; member omitted
    SVGFETurbulenceElement.SVG_TURBULENCE_TYPE_TURBULENCE: contract-only interface; member omitted
    SVGFETurbulenceElement.SVG_STITCHTYPE_UNKNOWN: contract-only interface; member omitted
    SVGFETurbulenceElement.SVG_STITCHTYPE_STITCH: contract-only interface; member omitted
    SVGFETurbulenceElement.SVG_STITCHTYPE_NOSTITCH: contract-only interface; member omitted
    SVGFETurbulenceElement.baseFrequencyX: contract-only interface; member omitted
    SVGFETurbulenceElement.baseFrequencyY: contract-only interface; member omitted
    SVGFETurbulenceElement.numOctaves: contract-only interface; member omitted
    SVGFETurbulenceElement.seed: contract-only interface; member omitted
    SVGFETurbulenceElement.stitchTiles: contract-only interface; member omitted
    SVGFETurbulenceElement.type: contract-only interface; member omitted
  SVGFilterElement: svg/SVGFilterElement.idl
    SVGFilterElement.filterUnits: contract-only interface; member omitted
    SVGFilterElement.primitiveUnits: contract-only interface; member omitted
    SVGFilterElement.x: contract-only interface; member omitted
    SVGFilterElement.y: contract-only interface; member omitted
    SVGFilterElement.width: contract-only interface; member omitted
    SVGFilterElement.height: contract-only interface; member omitted
  SVGForeignObjectElement: svg/SVGForeignObjectElement.idl
    SVGForeignObjectElement.x: contract-only interface; member omitted
    SVGForeignObjectElement.y: contract-only interface; member omitted
    SVGForeignObjectElement.width: contract-only interface; member omitted
    SVGForeignObjectElement.height: contract-only interface; member omitted
  SVGGElement: svg/SVGGElement.idl
  SVGImageElement: svg/SVGImageElement.idl
    SVGImageElement.x: contract-only interface; member omitted
    SVGImageElement.y: contract-only interface; member omitted
    SVGImageElement.width: contract-only interface; member omitted
    SVGImageElement.height: contract-only interface; member omitted
    SVGImageElement.preserveAspectRatio: contract-only interface; member omitted
    SVGImageElement.crossOrigin: contract-only interface; member omitted
    SVGImageElement.decode: contract-only interface; member omitted
  SVGLineElement: svg/SVGLineElement.idl
    SVGLineElement.x1: contract-only interface; member omitted
    SVGLineElement.y1: contract-only interface; member omitted
    SVGLineElement.x2: contract-only interface; member omitted
    SVGLineElement.y2: contract-only interface; member omitted
  SVGLinearGradientElement: svg/SVGLinearGradientElement.idl
    SVGLinearGradientElement.x1: contract-only interface; member omitted
    SVGLinearGradientElement.y1: contract-only interface; member omitted
    SVGLinearGradientElement.x2: contract-only interface; member omitted
    SVGLinearGradientElement.y2: contract-only interface; member omitted
  SVGMarkerElement: svg/SVGMarkerElement.idl
    SVGMarkerElement.SVG_MARKERUNITS_UNKNOWN: contract-only interface; member omitted
    SVGMarkerElement.SVG_MARKERUNITS_USERSPACEONUSE: contract-only interface; member omitted
    SVGMarkerElement.SVG_MARKERUNITS_STROKEWIDTH: contract-only interface; member omitted
    SVGMarkerElement.SVG_MARKER_ORIENT_UNKNOWN: contract-only interface; member omitted
    SVGMarkerElement.SVG_MARKER_ORIENT_AUTO: contract-only interface; member omitted
    SVGMarkerElement.SVG_MARKER_ORIENT_ANGLE: contract-only interface; member omitted
    SVGMarkerElement.SVG_MARKER_ORIENT_AUTO_START_REVERSE: contract-only interface; member omitted
    SVGMarkerElement.refX: contract-only interface; member omitted
    SVGMarkerElement.refY: contract-only interface; member omitted
    SVGMarkerElement.markerUnits: contract-only interface; member omitted
    SVGMarkerElement.markerWidth: contract-only interface; member omitted
    SVGMarkerElement.markerHeight: contract-only interface; member omitted
    SVGMarkerElement.orientType: contract-only interface; member omitted
    SVGMarkerElement.orientAngle: contract-only interface; member omitted
    SVGMarkerElement.orient: contract-only interface; member omitted
    SVGMarkerElement.setOrientToAuto: contract-only interface; member omitted
    SVGMarkerElement.setOrientToAngle: contract-only interface; member omitted
  SVGMaskElement: svg/SVGMaskElement.idl
    SVGMaskElement.maskUnits: contract-only interface; member omitted
    SVGMaskElement.maskContentUnits: contract-only interface; member omitted
    SVGMaskElement.x: contract-only interface; member omitted
    SVGMaskElement.y: contract-only interface; member omitted
    SVGMaskElement.width: contract-only interface; member omitted
    SVGMaskElement.height: contract-only interface; member omitted
  SVGMetadataElement: svg/SVGMetadataElement.idl
  SVGMPathElement: svg/SVGMPathElement.idl
  SVGPathElement: svg/SVGPathElement.idl
    SVGPathElement.getPathSegAtLength: contract-only interface; member omitted
    SVGPathElement.createSVGPathSegClosePath: contract-only interface; member omitted
    SVGPathElement.createSVGPathSegMovetoAbs: contract-only interface; member omitted
    SVGPathElement.createSVGPathSegMovetoRel: contract-only interface; member omitted
    SVGPathElement.createSVGPathSegLinetoAbs: contract-only interface; member omitted
    SVGPathElement.createSVGPathSegLinetoRel: contract-only interface; member omitted
    SVGPathElement.createSVGPathSegCurvetoCubicAbs: contract-only interface; member omitted
    SVGPathElement.createSVGPathSegCurvetoCubicRel: contract-only interface; member omitted
    SVGPathElement.createSVGPathSegCurvetoQuadraticAbs: contract-only interface; member omitted
    SVGPathElement.createSVGPathSegCurvetoQuadraticRel: contract-only interface; member omitted
    SVGPathElement.createSVGPathSegArcAbs: contract-only interface; member omitted
    SVGPathElement.createSVGPathSegArcRel: contract-only interface; member omitted
    SVGPathElement.createSVGPathSegLinetoHorizontalAbs: contract-only interface; member omitted
    SVGPathElement.createSVGPathSegLinetoHorizontalRel: contract-only interface; member omitted
    SVGPathElement.createSVGPathSegLinetoVerticalAbs: contract-only interface; member omitted
    SVGPathElement.createSVGPathSegLinetoVerticalRel: contract-only interface; member omitted
    SVGPathElement.createSVGPathSegCurvetoCubicSmoothAbs: contract-only interface; member omitted
    SVGPathElement.createSVGPathSegCurvetoCubicSmoothRel: contract-only interface; member omitted
    SVGPathElement.createSVGPathSegCurvetoQuadraticSmoothAbs: contract-only interface; member omitted
    SVGPathElement.createSVGPathSegCurvetoQuadraticSmoothRel: contract-only interface; member omitted
    SVGPathElement.pathSegList: contract-only interface; member omitted
    SVGPathElement.animatedPathSegList: contract-only interface; member omitted
  SVGPatternElement: svg/SVGPatternElement.idl
    SVGPatternElement.patternUnits: contract-only interface; member omitted
    SVGPatternElement.patternContentUnits: contract-only interface; member omitted
    SVGPatternElement.patternTransform: contract-only interface; member omitted
    SVGPatternElement.x: contract-only interface; member omitted
    SVGPatternElement.y: contract-only interface; member omitted
    SVGPatternElement.width: contract-only interface; member omitted
    SVGPatternElement.height: contract-only interface; member omitted
  SVGPolygonElement: svg/SVGPolygonElement.idl
    SVGPolygonElement.points: contract-only interface; member omitted
    SVGPolygonElement.animatedPoints: contract-only interface; member omitted
  SVGPolylineElement: svg/SVGPolylineElement.idl
    SVGPolylineElement.points: contract-only interface; member omitted
    SVGPolylineElement.animatedPoints: contract-only interface; member omitted
  SVGRadialGradientElement: svg/SVGRadialGradientElement.idl
    SVGRadialGradientElement.cx: contract-only interface; member omitted
    SVGRadialGradientElement.cy: contract-only interface; member omitted
    SVGRadialGradientElement.r: contract-only interface; member omitted
    SVGRadialGradientElement.fx: contract-only interface; member omitted
    SVGRadialGradientElement.fy: contract-only interface; member omitted
    SVGRadialGradientElement.fr: contract-only interface; member omitted
  SVGRectElement: svg/SVGRectElement.idl
    SVGRectElement.x: contract-only interface; member omitted
    SVGRectElement.y: contract-only interface; member omitted
    SVGRectElement.width: contract-only interface; member omitted
    SVGRectElement.height: contract-only interface; member omitted
    SVGRectElement.rx: contract-only interface; member omitted
    SVGRectElement.ry: contract-only interface; member omitted
  SVGScriptElement: svg/SVGScriptElement.idl
    SVGScriptElement.type: contract-only interface; member omitted
    SVGScriptElement.async: contract-only interface; member omitted
  SVGStopElement: svg/SVGStopElement.idl
    SVGStopElement.offset: contract-only interface; member omitted
  SVGStyleElement: svg/SVGStyleElement.idl
    SVGStyleElement.disabled: contract-only interface; member omitted
    SVGStyleElement.type: contract-only interface; member omitted
    SVGStyleElement.media: contract-only interface; member omitted
    SVGStyleElement.title: contract-only interface; member omitted
  SVGSVGElement: svg/SVGSVGElement.idl
    SVGSVGElement.x: contract-only interface; member omitted
    SVGSVGElement.y: contract-only interface; member omitted
    SVGSVGElement.width: contract-only interface; member omitted
    SVGSVGElement.height: contract-only interface; member omitted
    SVGSVGElement.currentScale: contract-only interface; member omitted
    SVGSVGElement.currentTranslate: contract-only interface; member omitted
    SVGSVGElement.useCurrentView: contract-only interface; member omitted
    SVGSVGElement.currentView: contract-only interface; member omitted
    SVGSVGElement.getIntersectionList: contract-only interface; member omitted
    SVGSVGElement.getEnclosureList: contract-only interface; member omitted
    SVGSVGElement.checkIntersection: contract-only interface; member omitted
    SVGSVGElement.checkEnclosure: contract-only interface; member omitted
    SVGSVGElement.deselectAll: contract-only interface; member omitted
    SVGSVGElement.createSVGNumber: contract-only interface; member omitted
    SVGSVGElement.createSVGLength: contract-only interface; member omitted
    SVGSVGElement.createSVGAngle: contract-only interface; member omitted
    SVGSVGElement.createSVGPoint: contract-only interface; member omitted
    SVGSVGElement.createSVGMatrix: contract-only interface; member omitted
    SVGSVGElement.createSVGRect: contract-only interface; member omitted
    SVGSVGElement.createSVGTransform: contract-only interface; member omitted
    SVGSVGElement.createSVGTransformFromMatrix: contract-only interface; member omitted
    SVGSVGElement.getElementById: contract-only interface; member omitted
    SVGSVGElement.pauseAnimations: contract-only interface; member omitted
    SVGSVGElement.unpauseAnimations: contract-only interface; member omitted
    SVGSVGElement.animationsPaused: contract-only interface; member omitted
    SVGSVGElement.getCurrentTime: contract-only interface; member omitted
    SVGSVGElement.setCurrentTime: contract-only interface; member omitted
    SVGSVGElement.suspendRedraw: contract-only interface; member omitted
    SVGSVGElement.unsuspendRedraw: contract-only interface; member omitted
    SVGSVGElement.unsuspendRedrawAll: contract-only interface; member omitted
    SVGSVGElement.forceRedraw: contract-only interface; member omitted
  SVGSwitchElement: svg/SVGSwitchElement.idl
  SVGSymbolElement: svg/SVGSymbolElement.idl
  SVGTextElement: svg/SVGTextElement.idl
  SVGTextPathElement: svg/SVGTextPathElement.idl
    SVGTextPathElement.TEXTPATH_METHODTYPE_UNKNOWN: contract-only interface; member omitted
    SVGTextPathElement.TEXTPATH_METHODTYPE_ALIGN: contract-only interface; member omitted
    SVGTextPathElement.TEXTPATH_METHODTYPE_STRETCH: contract-only interface; member omitted
    SVGTextPathElement.TEXTPATH_SPACINGTYPE_UNKNOWN: contract-only interface; member omitted
    SVGTextPathElement.TEXTPATH_SPACINGTYPE_AUTO: contract-only interface; member omitted
    SVGTextPathElement.TEXTPATH_SPACINGTYPE_EXACT: contract-only interface; member omitted
    SVGTextPathElement.startOffset: contract-only interface; member omitted
    SVGTextPathElement.method: contract-only interface; member omitted
    SVGTextPathElement.spacing: contract-only interface; member omitted
  SVGTitleElement: svg/SVGTitleElement.idl
  SVGTSpanElement: svg/SVGTSpanElement.idl
  SVGUseElement: svg/SVGUseElement.idl
    SVGUseElement.x: contract-only interface; member omitted
    SVGUseElement.y: contract-only interface; member omitted
    SVGUseElement.width: contract-only interface; member omitted
    SVGUseElement.height: contract-only interface; member omitted
  SVGViewElement: svg/SVGViewElement.idl
  AnimationEvent: animation/CSSAnimationEvent.idl
    CSSAnimationEvent.constructor: contract-only interface; member omitted
    CSSAnimationEvent.animation: contract-only interface; member omitted
    CSSAnimationEvent.animationName: contract-only interface; member omitted
    CSSAnimationEvent.elapsedTime: contract-only interface; member omitted
    CSSAnimationEvent.pseudoElement: contract-only interface; member omitted
  TransitionEvent: animation/CSSTransitionEvent.idl
    CSSTransitionEvent.constructor: contract-only interface; member omitted
    CSSTransitionEvent.animation: contract-only interface; member omitted
    CSSTransitionEvent.propertyName: contract-only interface; member omitted
    CSSTransitionEvent.elapsedTime: contract-only interface; member omitted
    CSSTransitionEvent.pseudoElement: contract-only interface; member omitted
  BeforeUnloadEvent: dom/BeforeUnloadEvent.idl
    BeforeUnloadEvent.returnValue: contract-only interface; member omitted
  CSSStyleSheet: css/CSSStyleSheet.idl
    CSSStyleSheet.constructor: contract-only interface; member omitted
    CSSStyleSheet.ownerRule: contract-only interface; member omitted
    CSSStyleSheet.cssRules: contract-only interface; member omitted
    CSSStyleSheet.insertRule: contract-only interface; member omitted
    CSSStyleSheet.deleteRule: contract-only interface; member omitted
    CSSStyleSheet.replace: contract-only interface; member omitted
    CSSStyleSheet.replaceSync: contract-only interface; member omitted
    CSSStyleSheet.rules: contract-only interface; member omitted
    CSSStyleSheet.addRule: contract-only interface; member omitted
    CSSStyleSheet.removeRule: contract-only interface; member omitted
  CSSStyleProperties: css/CSSStyleProperties.idl
    CSSStyleProperties.cssFloat: contract-only interface; member omitted
  CustomElementRegistry: dom/CustomElementRegistry.idl
    CustomElementRegistry.constructor: contract-only interface; member omitted
    CustomElementRegistry.define: contract-only interface; member omitted
    CustomElementRegistry.get: contract-only interface; member omitted
    CustomElementRegistry.getName: contract-only interface; member omitted
    CustomElementRegistry.whenDefined: contract-only interface; member omitted
    CustomElementRegistry.upgrade: contract-only interface; member omitted
    CustomElementRegistry.initialize: contract-only interface; member omitted
  ClipboardEvent: dom/ClipboardEvent.idl
    ClipboardEvent.constructor: contract-only interface; member omitted
    ClipboardEvent.clipboardData: contract-only interface; member omitted
  DragEvent: dom/DragEvent.idl
    DragEvent.constructor: contract-only interface; member omitted
    DragEvent.dataTransfer: contract-only interface; member omitted
  ErrorEvent: dom/ErrorEvent.idl
    ErrorEvent.constructor: contract-only interface; member omitted
    ErrorEvent.message: contract-only interface; member omitted
    ErrorEvent.filename: contract-only interface; member omitted
    ErrorEvent.lineno: contract-only interface; member omitted
    ErrorEvent.colno: contract-only interface; member omitted
    ErrorEvent.error: contract-only interface; member omitted
  FileList: fileapi/FileList.idl
    FileList.length: contract-only interface; member omitted
    FileList.item: contract-only interface; member omitted
  FormDataEvent: dom/FormDataEvent.idl
    FormDataEvent.constructor: contract-only interface; member omitted
    FormDataEvent.formData: contract-only interface; member omitted
  GamepadEvent: Modules/gamepad/GamepadEvent.idl
    GamepadEvent.constructor: contract-only interface; member omitted
    GamepadEvent.gamepad: contract-only interface; member omitted
  HTMLFormControlsCollection: html/HTMLFormControlsCollection.idl
    HTMLFormControlsCollection.getter: contract-only interface; member omitted
    HTMLFormControlsCollection.namedItem: contract-only interface; member omitted
  HTMLOptionsCollection: html/HTMLOptionsCollection.idl
    HTMLOptionsCollection.length: contract-only interface; member omitted
    HTMLOptionsCollection.setter: contract-only interface; member omitted
    HTMLOptionsCollection.add: contract-only interface; member omitted
    HTMLOptionsCollection.remove: contract-only interface; member omitted
    HTMLOptionsCollection.selectedIndex: contract-only interface; member omitted
    HTMLOptionsCollection.item: contract-only interface; member omitted
    HTMLOptionsCollection.namedItem: contract-only interface; member omitted
  HashChangeEvent: dom/HashChangeEvent.idl
    HashChangeEvent.constructor: contract-only interface; member omitted
    HashChangeEvent.oldURL: contract-only interface; member omitted
    HashChangeEvent.newURL: contract-only interface; member omitted
  MediaEncryptedEvent: html/MediaEncryptedEvent.idl
    MediaEncryptedEvent.constructor: contract-only interface; member omitted
    MediaEncryptedEvent.initDataType: contract-only interface; member omitted
    MediaEncryptedEvent.initData: contract-only interface; member omitted
  MediaError: html/MediaError.idl
    MediaError.MEDIA_ERR_ABORTED: contract-only interface; member omitted
    MediaError.MEDIA_ERR_NETWORK: contract-only interface; member omitted
    MediaError.MEDIA_ERR_DECODE: contract-only interface; member omitted
    MediaError.MEDIA_ERR_SRC_NOT_SUPPORTED: contract-only interface; member omitted
    MediaError.MEDIA_ERR_ENCRYPTED: contract-only interface; member omitted
    MediaError.code: contract-only interface; member omitted
    MediaError.message: contract-only interface; member omitted
  MediaKeys: Modules/encryptedmedia/MediaKeys.idl
    MediaKeys.createSession: contract-only interface; member omitted
    MediaKeys.setServerCertificate: contract-only interface; member omitted
  PageRevealEvent: dom/PageRevealEvent.idl
    PageRevealEvent.constructor: contract-only interface; member omitted
    PageRevealEvent.viewTransition: contract-only interface; member omitted
  PageSwapEvent: dom/PageSwapEvent.idl
    PageSwapEvent.constructor: contract-only interface; member omitted
    PageSwapEvent.activation: contract-only interface; member omitted
    PageSwapEvent.viewTransition: contract-only interface; member omitted
  PageTransitionEvent: dom/PageTransitionEvent.idl
    PageTransitionEvent.constructor: contract-only interface; member omitted
    PageTransitionEvent.persisted: contract-only interface; member omitted
  PictureInPictureEvent: Modules/pictureinpicture/PictureInPictureEvent.idl
    PictureInPictureEvent.constructor: contract-only interface; member omitted
    PictureInPictureEvent.pictureInPictureWindow: contract-only interface; member omitted
  PopStateEvent: dom/PopStateEvent.idl
    PopStateEvent.constructor: contract-only interface; member omitted
    PopStateEvent.state: contract-only interface; member omitted
    PopStateEvent.hasUAVisualTransition: contract-only interface; member omitted
  ProgressEvent: dom/ProgressEvent.idl
    ProgressEvent.constructor: contract-only interface; member omitted
    ProgressEvent.lengthComputable: contract-only interface; member omitted
    ProgressEvent.loaded: contract-only interface; member omitted
    ProgressEvent.total: contract-only interface; member omitted
  PromiseRejectionEvent: dom/PromiseRejectionEvent.idl
    PromiseRejectionEvent.constructor: contract-only interface; member omitted
    PromiseRejectionEvent.promise: contract-only interface; member omitted
    PromiseRejectionEvent.reason: contract-only interface; member omitted
  RemotePlayback: Modules/remoteplayback/RemotePlayback.idl
    RemotePlayback.watchAvailability: contract-only interface; member omitted
    RemotePlayback.cancelWatchAvailability: contract-only interface; member omitted
    RemotePlayback.state: contract-only interface; member omitted
    RemotePlayback.onconnecting: contract-only interface; member omitted
    RemotePlayback.onconnect: contract-only interface; member omitted
    RemotePlayback.ondisconnect: contract-only interface; member omitted
    RemotePlayback.prompt: contract-only interface; member omitted
  SecurityPolicyViolationEvent: dom/SecurityPolicyViolationEvent.idl
    SecurityPolicyViolationEvent.constructor: contract-only interface; member omitted
    SecurityPolicyViolationEvent.constructor: contract-only interface; member omitted
    SecurityPolicyViolationEvent.documentURI: contract-only interface; member omitted
    SecurityPolicyViolationEvent.referrer: contract-only interface; member omitted
    SecurityPolicyViolationEvent.blockedURI: contract-only interface; member omitted
    SecurityPolicyViolationEvent.effectiveDirective: contract-only interface; member omitted
    SecurityPolicyViolationEvent.violatedDirective: contract-only interface; member omitted
    SecurityPolicyViolationEvent.originalPolicy: contract-only interface; member omitted
    SecurityPolicyViolationEvent.sourceFile: contract-only interface; member omitted
    SecurityPolicyViolationEvent.sample: contract-only interface; member omitted
    SecurityPolicyViolationEvent.disposition: contract-only interface; member omitted
    SecurityPolicyViolationEvent.statusCode: contract-only interface; member omitted
    SecurityPolicyViolationEvent.lineno: contract-only interface; member omitted
    SecurityPolicyViolationEvent.lineNumber: contract-only interface; member omitted
    SecurityPolicyViolationEvent.colno: contract-only interface; member omitted
    SecurityPolicyViolationEvent.columnNumber: contract-only interface; member omitted
  StorageEvent: storage/StorageEvent.idl
    StorageEvent.constructor: contract-only interface; member omitted
    StorageEvent.key: contract-only interface; member omitted
    StorageEvent.oldValue: contract-only interface; member omitted
    StorageEvent.newValue: contract-only interface; member omitted
    StorageEvent.url: contract-only interface; member omitted
    StorageEvent.storageArea: contract-only interface; member omitted
    StorageEvent.initStorageEvent: contract-only interface; member omitted
  StylePropertyMap: css/typedom/StylePropertyMap.idl
    StylePropertyMap.set: contract-only interface; member omitted
    StylePropertyMap.append: contract-only interface; member omitted
    StylePropertyMap.delete: contract-only interface; member omitted
    StylePropertyMap.clear: contract-only interface; member omitted
  SubmitEvent: html/SubmitEvent.idl
    SubmitEvent.constructor: contract-only interface; member omitted
    SubmitEvent.submitter: contract-only interface; member omitted
  TextTrack: html/track/TextTrack.idl
    TextTrack.kind: contract-only interface; member omitted
    TextTrack.label: contract-only interface; member omitted
    TextTrack.language: contract-only interface; member omitted
    TextTrack.id: contract-only interface; member omitted
    TextTrack.inBandMetadataTrackDispatchType: contract-only interface; member omitted
    TextTrack.mode: contract-only interface; member omitted
    TextTrack.cues: contract-only interface; member omitted
    TextTrack.activeCues: contract-only interface; member omitted
    TextTrack.addCue: contract-only interface; member omitted
    TextTrack.removeCue: contract-only interface; member omitted
    TextTrack.oncuechange: contract-only interface; member omitted
  TextTrackList: html/track/TextTrackList.idl
    TextTrackList.length: contract-only interface; member omitted
    TextTrackList.item: contract-only interface; member omitted
    TextTrackList.getTrackById: contract-only interface; member omitted
    TextTrackList.onaddtrack: contract-only interface; member omitted
    TextTrackList.onchange: contract-only interface; member omitted
    TextTrackList.onremovetrack: contract-only interface; member omitted
  TimeRanges: html/TimeRanges.idl
    TimeRanges.length: contract-only interface; member omitted
    TimeRanges.start: contract-only interface; member omitted
    TimeRanges.end: contract-only interface; member omitted
  ToggleEvent: dom/ToggleEvent.idl
    ToggleEvent.constructor: contract-only interface; member omitted
    ToggleEvent.oldState: contract-only interface; member omitted
    ToggleEvent.newState: contract-only interface; member omitted
    ToggleEvent.source: contract-only interface; member omitted
  TouchEvent: dom/TouchEvent.idl
    TouchEvent.constructor: contract-only interface; member omitted
    TouchEvent.touches: contract-only interface; member omitted
    TouchEvent.targetTouches: contract-only interface; member omitted
    TouchEvent.changedTouches: contract-only interface; member omitted
    TouchEvent.ctrlKey: contract-only interface; member omitted
    TouchEvent.shiftKey: contract-only interface; member omitted
    TouchEvent.altKey: contract-only interface; member omitted
    TouchEvent.metaKey: contract-only interface; member omitted

Contract mixins (only members mapped by scoped concrete interfaces are declared):
  ARIAMixin: accessibility/AriaAttributes.idl
    ARIAMixin.ariaActionsElements: no scoped concrete native export; member omitted
    ARIAMixin.ariaControlsElements: no scoped concrete native export; member omitted
    ARIAMixin.ariaDescribedByElements: no scoped concrete native export; member omitted
    ARIAMixin.ariaDetailsElements: no scoped concrete native export; member omitted
    ARIAMixin.ariaErrorMessageElements: no scoped concrete native export; member omitted
    ARIAMixin.ariaFlowToElements: no scoped concrete native export; member omitted
    ARIAMixin.ariaLabelledByElements: no scoped concrete native export; member omitted
    ARIAMixin.ariaOwnsElements: no scoped concrete native export; member omitted
  ChildNode: dom/ChildNode.idl
  ParentNode: dom/ParentNode.idl
  NonDocumentTypeChildNode: dom/NonDocumentTypeChildNode.idl
  GlobalEventHandlers: dom/GlobalEventHandlers.idl
    GlobalEventHandlers.onabort: no scoped concrete native export; member omitted
    GlobalEventHandlers.onauxclick: no scoped concrete native export; member omitted
    GlobalEventHandlers.onbeforeinput: no scoped concrete native export; member omitted
    GlobalEventHandlers.onbeforematch: no scoped concrete native export; member omitted
    GlobalEventHandlers.onbeforetoggle: no scoped concrete native export; member omitted
    GlobalEventHandlers.onblur: no scoped concrete native export; member omitted
    GlobalEventHandlers.oncancel: no scoped concrete native export; member omitted
    GlobalEventHandlers.oncanplay: no scoped concrete native export; member omitted
    GlobalEventHandlers.oncanplaythrough: no scoped concrete native export; member omitted
    GlobalEventHandlers.onchange: no scoped concrete native export; member omitted
    GlobalEventHandlers.onclick: no scoped concrete native export; member omitted
    GlobalEventHandlers.onclose: no scoped concrete native export; member omitted
    GlobalEventHandlers.oncommand: no scoped concrete native export; member omitted
    GlobalEventHandlers.oncontentvisibilityautostatechange: no scoped concrete native export; member omitted
    GlobalEventHandlers.oncontextmenu: no scoped concrete native export; member omitted
    GlobalEventHandlers.oncopy: no scoped concrete native export; member omitted
    GlobalEventHandlers.oncuechange: no scoped concrete native export; member omitted
    GlobalEventHandlers.oncut: no scoped concrete native export; member omitted
    GlobalEventHandlers.ondblclick: no scoped concrete native export; member omitted
    GlobalEventHandlers.ondrag: no scoped concrete native export; member omitted
    GlobalEventHandlers.ondragend: no scoped concrete native export; member omitted
    GlobalEventHandlers.ondragenter: no scoped concrete native export; member omitted
    GlobalEventHandlers.ondragleave: no scoped concrete native export; member omitted
    GlobalEventHandlers.ondragover: no scoped concrete native export; member omitted
    GlobalEventHandlers.ondragstart: no scoped concrete native export; member omitted
    GlobalEventHandlers.ondrop: no scoped concrete native export; member omitted
    GlobalEventHandlers.ondurationchange: no scoped concrete native export; member omitted
    GlobalEventHandlers.onemptied: no scoped concrete native export; member omitted
    GlobalEventHandlers.onended: no scoped concrete native export; member omitted
    GlobalEventHandlers.onerror: no scoped concrete native export; member omitted
    GlobalEventHandlers.onfocus: no scoped concrete native export; member omitted
    GlobalEventHandlers.onformdata: no scoped concrete native export; member omitted
    GlobalEventHandlers.oninput: no scoped concrete native export; member omitted
    GlobalEventHandlers.oninvalid: no scoped concrete native export; member omitted
    GlobalEventHandlers.onkeydown: no scoped concrete native export; member omitted
    GlobalEventHandlers.onkeypress: no scoped concrete native export; member omitted
    GlobalEventHandlers.onkeyup: no scoped concrete native export; member omitted
    GlobalEventHandlers.onload: no scoped concrete native export; member omitted
    GlobalEventHandlers.onloadeddata: no scoped concrete native export; member omitted
    GlobalEventHandlers.onloadedmetadata: no scoped concrete native export; member omitted
    GlobalEventHandlers.onloadstart: no scoped concrete native export; member omitted
    GlobalEventHandlers.onmousedown: no scoped concrete native export; member omitted
    GlobalEventHandlers.onmouseenter: no scoped concrete native export; member omitted
    GlobalEventHandlers.onmouseleave: no scoped concrete native export; member omitted
    GlobalEventHandlers.onmousemove: no scoped concrete native export; member omitted
    GlobalEventHandlers.onmouseout: no scoped concrete native export; member omitted
    GlobalEventHandlers.onmouseover: no scoped concrete native export; member omitted
    GlobalEventHandlers.onmouseup: no scoped concrete native export; member omitted
    GlobalEventHandlers.onpaste: no scoped concrete native export; member omitted
    GlobalEventHandlers.onpause: no scoped concrete native export; member omitted
    GlobalEventHandlers.onplay: no scoped concrete native export; member omitted
    GlobalEventHandlers.onplaying: no scoped concrete native export; member omitted
    GlobalEventHandlers.onprogress: no scoped concrete native export; member omitted
    GlobalEventHandlers.onratechange: no scoped concrete native export; member omitted
    GlobalEventHandlers.onreset: no scoped concrete native export; member omitted
    GlobalEventHandlers.onresize: no scoped concrete native export; member omitted
    GlobalEventHandlers.onscroll: no scoped concrete native export; member omitted
    GlobalEventHandlers.onscrollend: no scoped concrete native export; member omitted
    GlobalEventHandlers.onsecuritypolicyviolation: no scoped concrete native export; member omitted
    GlobalEventHandlers.onseeked: no scoped concrete native export; member omitted
    GlobalEventHandlers.onseeking: no scoped concrete native export; member omitted
    GlobalEventHandlers.onselect: no scoped concrete native export; member omitted
    GlobalEventHandlers.onslotchange: no scoped concrete native export; member omitted
    GlobalEventHandlers.onstalled: no scoped concrete native export; member omitted
    GlobalEventHandlers.onsubmit: no scoped concrete native export; member omitted
    GlobalEventHandlers.onsuspend: no scoped concrete native export; member omitted
    GlobalEventHandlers.ontimeupdate: no scoped concrete native export; member omitted
    GlobalEventHandlers.ontoggle: no scoped concrete native export; member omitted
    GlobalEventHandlers.onvolumechange: no scoped concrete native export; member omitted
    GlobalEventHandlers.onwaiting: no scoped concrete native export; member omitted
    GlobalEventHandlers.onwebkitanimationend: no scoped concrete native export; member omitted
    GlobalEventHandlers.onwebkitanimationiteration: no scoped concrete native export; member omitted
    GlobalEventHandlers.onwebkitanimationstart: no scoped concrete native export; member omitted
    GlobalEventHandlers.onwebkittransitionend: no scoped concrete native export; member omitted
    GlobalEventHandlers.onwheel: no scoped concrete native export; member omitted
    GlobalEventHandlers.onmousewheel: no scoped concrete native export; member omitted
    GlobalEventHandlers.ontouchcancel: no scoped concrete native export; member omitted
    GlobalEventHandlers.ontouchend: no scoped concrete native export; member omitted
    GlobalEventHandlers.ontouchmove: no scoped concrete native export; member omitted
    GlobalEventHandlers.ontouchstart: no scoped concrete native export; member omitted
    GlobalEventHandlers.ontouchforcechange: no scoped concrete native export; member omitted
    GlobalEventHandlers.onwebkitmouseforcechanged: no scoped concrete native export; member omitted
    GlobalEventHandlers.onwebkitmouseforcedown: no scoped concrete native export; member omitted
    GlobalEventHandlers.onwebkitmouseforcewillbegin: no scoped concrete native export; member omitted
    GlobalEventHandlers.onwebkitmouseforceup: no scoped concrete native export; member omitted
    GlobalEventHandlers.onanimationstart: no scoped concrete native export; member omitted
    GlobalEventHandlers.onanimationiteration: no scoped concrete native export; member omitted
    GlobalEventHandlers.onanimationend: no scoped concrete native export; member omitted
    GlobalEventHandlers.onanimationcancel: no scoped concrete native export; member omitted
    GlobalEventHandlers.ontransitionrun: no scoped concrete native export; member omitted
    GlobalEventHandlers.ontransitionstart: no scoped concrete native export; member omitted
    GlobalEventHandlers.ontransitionend: no scoped concrete native export; member omitted
    GlobalEventHandlers.ontransitioncancel: no scoped concrete native export; member omitted
    GlobalEventHandlers.ongotpointercapture: no scoped concrete native export; member omitted
    GlobalEventHandlers.onlostpointercapture: no scoped concrete native export; member omitted
    GlobalEventHandlers.onpointerdown: no scoped concrete native export; member omitted
    GlobalEventHandlers.onpointermove: no scoped concrete native export; member omitted
    GlobalEventHandlers.onpointerup: no scoped concrete native export; member omitted
    GlobalEventHandlers.onpointercancel: no scoped concrete native export; member omitted
    GlobalEventHandlers.onpointerover: no scoped concrete native export; member omitted
    GlobalEventHandlers.onpointerout: no scoped concrete native export; member omitted
    GlobalEventHandlers.onpointerenter: no scoped concrete native export; member omitted
    GlobalEventHandlers.onpointerleave: no scoped concrete native export; member omitted
    GlobalEventHandlers.onselectstart: no scoped concrete native export; member omitted
    GlobalEventHandlers.onselectionchange: no scoped concrete native export; member omitted
  ElementCSSInlineStyle: css/ElementCSSInlineStyle.idl
    ElementCSSInlineStyle.attributeStyleMap: no scoped concrete native export; member omitted
  ElementContentEditable: dom/ElementContentEditable.idl
  HTMLHyperlinkElementUtils: html/HTMLHyperlinkElementUtils.idl
  HTMLOrSVGElement: html/HTMLOrSVGOrMathMLElement.idl
  LinkStyle: css/LinkStyle.idl
    LinkStyle.sheet: no scoped concrete native export; member omitted
  PopoverTargetAttributes: html/PopoverInvokerElement.idl
  Slottable: dom/Slotable.idl
```
