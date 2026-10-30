
import { Union } from "../../../../../../../../../fable_modules/fable-library-js.5.24.0/Types.js";
import { unit_type, int32_type, class_type, float64_type, bool_type, union_type, string_type } from "../../../../../../../../../fable_modules/fable-library-js.5.24.0/Reflection.js";
import { some, defaultArg, unwrap } from "../../../../../../../../../fable_modules/fable-library-js.5.24.0/Option.js";
import { Exception, defaultOf } from "../../../../../../../../../fable_modules/fable-library-js.5.24.0/Util.js";
import { Operators_NullArg, Operators_IsNull } from "../../../../../../../../../fable_modules/fable-library-js.5.24.0/FSharp.Core.js";
import { ArgumentException } from "../../../../../../../../../fable_modules/fable-library-js.5.24.0/System.js";
import { map } from "../../../../../../../../../fable_modules/fable-library-js.5.24.0/Array.js";

/**
 * DOMStringOrElementCreationOptions in the scoped DOM interface model.
 */
export class DOMStringOrElementCreationOptions extends Union {
    constructor(tag, fields) {
        super();
        this.tag = tag;
        this.fields = fields;
    }
    cases() {
        return ["DOMString", "ElementCreationOptions"];
    }
}

export function DOMStringOrElementCreationOptions_$reflection() {
    return union_type("Ranvier.Dom.DOMStringOrElementCreationOptions", [], DOMStringOrElementCreationOptions, () => [[["Item", string_type]], [["Item", ElementCreationOptions_$reflection()]]]);
}

/**
 * BooleanOrImportNodeOptions in the scoped DOM interface model.
 */
export class BooleanOrImportNodeOptions extends Union {
    constructor(tag, fields) {
        super();
        this.tag = tag;
        this.fields = fields;
    }
    cases() {
        return ["Boolean", "ImportNodeOptions"];
    }
}

export function BooleanOrImportNodeOptions_$reflection() {
    return union_type("Ranvier.Dom.BooleanOrImportNodeOptions", [], BooleanOrImportNodeOptions, () => [[["Item", bool_type]], [["Item", ImportNodeOptions_$reflection()]]]);
}

/**
 * BooleanOrScrollIntoViewOptions in the scoped DOM interface model.
 */
export class BooleanOrScrollIntoViewOptions extends Union {
    constructor(tag, fields) {
        super();
        this.tag = tag;
        this.fields = fields;
    }
    cases() {
        return ["Boolean", "ScrollIntoViewOptions"];
    }
}

export function BooleanOrScrollIntoViewOptions_$reflection() {
    return union_type("Ranvier.Dom.BooleanOrScrollIntoViewOptions", [], BooleanOrScrollIntoViewOptions, () => [[["Item", bool_type]], [["Item", ScrollIntoViewOptions_$reflection()]]]);
}

/**
 * BooleanOrUnrestrictedDoubleOrDOMString in the scoped DOM interface model.
 */
export class BooleanOrUnrestrictedDoubleOrDOMString extends Union {
    constructor(tag, fields) {
        super();
        this.tag = tag;
        this.fields = fields;
    }
    cases() {
        return ["Boolean", "UnrestrictedDouble", "DOMString"];
    }
}

export function BooleanOrUnrestrictedDoubleOrDOMString_$reflection() {
    return union_type("Ranvier.Dom.BooleanOrUnrestrictedDoubleOrDOMString", [], BooleanOrUnrestrictedDoubleOrDOMString, () => [[["Item", bool_type]], [["Item", float64_type]], [["Item", string_type]]]);
}

/**
 * TogglePopoverOptionsOrBoolean in the scoped DOM interface model.
 */
export class TogglePopoverOptionsOrBoolean extends Union {
    constructor(tag, fields) {
        super();
        this.tag = tag;
        this.fields = fields;
    }
    cases() {
        return ["TogglePopoverOptions", "Boolean"];
    }
}

export function TogglePopoverOptionsOrBoolean_$reflection() {
    return union_type("Ranvier.Dom.TogglePopoverOptionsOrBoolean", [], TogglePopoverOptionsOrBoolean, () => [[["Item", TogglePopoverOptions_$reflection()]], [["Item", bool_type]]]);
}

/**
 * RadioNodeListOrElement in the scoped DOM interface model.
 */
export class RadioNodeListOrElement extends Union {
    constructor(tag, fields) {
        super();
        this.tag = tag;
        this.fields = fields;
    }
    cases() {
        return ["RadioNodeList", "Element"];
    }
}

export function RadioNodeListOrElement_$reflection() {
    return union_type("Ranvier.Dom.RadioNodeListOrElement", [], RadioNodeListOrElement, () => [[["Item", class_type("Ranvier.Dom.RadioNodeList")]], [["Item", class_type("Ranvier.Dom.Element")]]]);
}

/**
 * HTMLOptionElementOrHTMLOptGroupElement in the scoped DOM interface model.
 */
export class HTMLOptionElementOrHTMLOptGroupElement extends Union {
    constructor(tag, fields) {
        super();
        this.tag = tag;
        this.fields = fields;
    }
    cases() {
        return ["HTMLOptionElement", "HTMLOptGroupElement"];
    }
}

export function HTMLOptionElementOrHTMLOptGroupElement_$reflection() {
    return union_type("Ranvier.Dom.HTMLOptionElementOrHTMLOptGroupElement", [], HTMLOptionElementOrHTMLOptGroupElement, () => [[["Item", class_type("Ranvier.Dom.HTMLOptionElement")]], [["Item", class_type("Ranvier.Dom.HTMLOptGroupElement")]]]);
}

/**
 * HTMLElementOrLong in the scoped DOM interface model.
 */
export class HTMLElementOrLong extends Union {
    constructor(tag, fields) {
        super();
        this.tag = tag;
        this.fields = fields;
    }
    cases() {
        return ["HTMLElement", "Long"];
    }
}

export function HTMLElementOrLong_$reflection() {
    return union_type("Ranvier.Dom.HTMLElementOrLong", [], HTMLElementOrLong, () => [[["Item", class_type("Ranvier.Dom.HTMLElement")]], [["Item", int32_type]]]);
}

/**
 * ElementOrText in the scoped DOM interface model.
 */
export class ElementOrText extends Union {
    constructor(tag, fields) {
        super();
        this.tag = tag;
        this.fields = fields;
    }
    cases() {
        return ["Element", "Text"];
    }
}

export function ElementOrText_$reflection() {
    return union_type("Ranvier.Dom.ElementOrText", [], ElementOrText, () => [[["Item", class_type("Ranvier.Dom.Element")]], [["Item", class_type("Ranvier.Dom.Text")]]]);
}

/**
 * DoubleOrCSSNumericValue in the scoped DOM interface model.
 */
export class DoubleOrCSSNumericValue extends Union {
    constructor(tag, fields) {
        super();
        this.tag = tag;
        this.fields = fields;
    }
    cases() {
        return ["Double", "CSSNumericValue"];
    }
}

export function DoubleOrCSSNumericValue_$reflection() {
    return union_type("Ranvier.Dom.DoubleOrCSSNumericValue", [], DoubleOrCSSNumericValue, () => [[["Item", float64_type]], [["Item", class_type("Ranvier.Dom.CSSNumericValue")]]]);
}

/**
 * CSSNumericValueOrDOMStringOrCSSOMKeywordValue in the scoped DOM interface model.
 */
export class CSSNumericValueOrDOMStringOrCSSOMKeywordValue extends Union {
    constructor(tag, fields) {
        super();
        this.tag = tag;
        this.fields = fields;
    }
    cases() {
        return ["CSSNumericValue", "DOMString", "CSSOMKeywordValue"];
    }
}

export function CSSNumericValueOrDOMStringOrCSSOMKeywordValue_$reflection() {
    return union_type("Ranvier.Dom.CSSNumericValueOrDOMStringOrCSSOMKeywordValue", [], CSSNumericValueOrDOMStringOrCSSOMKeywordValue, () => [[["Item", class_type("Ranvier.Dom.CSSNumericValue")]], [["Item", string_type]], [["Item", class_type("Ranvier.Dom.CSSKeywordValue")]]]);
}

/**
 * USVStringOrCSSOMVariableReferenceValue in the scoped DOM interface model.
 */
export class USVStringOrCSSOMVariableReferenceValue extends Union {
    constructor(tag, fields) {
        super();
        this.tag = tag;
        this.fields = fields;
    }
    cases() {
        return ["USVString", "CSSOMVariableReferenceValue"];
    }
}

export function USVStringOrCSSOMVariableReferenceValue_$reflection() {
    return union_type("Ranvier.Dom.USVStringOrCSSOMVariableReferenceValue", [], USVStringOrCSSOMVariableReferenceValue, () => [[["Item", string_type]], [["Item", class_type("Ranvier.Dom.CSSVariableReferenceValue")]]]);
}

/**
 * UndefinedOrCSSStyleValue in the scoped DOM interface model.
 */
export class UndefinedOrCSSStyleValue extends Union {
    constructor(tag, fields) {
        super();
        this.tag = tag;
        this.fields = fields;
    }
    cases() {
        return ["Undefined", "CSSStyleValue"];
    }
}

export function UndefinedOrCSSStyleValue_$reflection() {
    return union_type("Ranvier.Dom.UndefinedOrCSSStyleValue", [], UndefinedOrCSSStyleValue, () => [[["Item", unit_type]], [["Item", class_type("Ranvier.Dom.CSSStyleValue")]]]);
}

/**
 * CSSStyleValueOrUSVString in the scoped DOM interface model.
 */
export class CSSStyleValueOrUSVString extends Union {
    constructor(tag, fields) {
        super();
        this.tag = tag;
        this.fields = fields;
    }
    cases() {
        return ["CSSStyleValue", "USVString"];
    }
}

export function CSSStyleValueOrUSVString_$reflection() {
    return union_type("Ranvier.Dom.CSSStyleValueOrUSVString", [], CSSStyleValueOrUSVString, () => [[["Item", class_type("Ranvier.Dom.CSSStyleValue")]], [["Item", string_type]]]);
}

/**
 * ShadowRootOrGetComposedRangesOptions in the scoped DOM interface model.
 */
export class ShadowRootOrGetComposedRangesOptions extends Union {
    constructor(tag, fields) {
        super();
        this.tag = tag;
        this.fields = fields;
    }
    cases() {
        return ["ShadowRoot", "GetComposedRangesOptions"];
    }
}

export function ShadowRootOrGetComposedRangesOptions_$reflection() {
    return union_type("Ranvier.Dom.ShadowRootOrGetComposedRangesOptions", [], ShadowRootOrGetComposedRangesOptions, () => [[["Item", class_type("Ranvier.Dom.ShadowRoot")]], [["Item", GetComposedRangesOptions_$reflection()]]]);
}

/**
 * NodeOrDOMString in the scoped DOM interface model.
 */
export class NodeOrDOMString extends Union {
    constructor(tag, fields) {
        super();
        this.tag = tag;
        this.fields = fields;
    }
    cases() {
        return ["Node", "DOMString"];
    }
}

export function NodeOrDOMString_$reflection() {
    return union_type("Ranvier.Dom.NodeOrDOMString", [], NodeOrDOMString, () => [[["Item", class_type("Ranvier.Dom.Node")]], [["Item", string_type]]]);
}

/**
 * EventListenerOptions in the scoped DOM interface model.
 */
export class EventListenerOptions {
    constructor() {
        this["capture@"] = false;
    }
    get capture() {
        const __ = this;
        return __["capture@"];
    }
    set capture(v) {
        const __ = this;
        __["capture@"] = v;
    }
}

export function EventListenerOptions_$reflection() {
    return class_type("Ranvier.Dom.EventListenerOptions", undefined, EventListenerOptions);
}

export function EventListenerOptions_$ctor() {
    return new EventListenerOptions();
}

/**
 * AddEventListenerOptions in the scoped DOM interface model.
 */
export class AddEventListenerOptions extends EventListenerOptions {
    constructor() {
        super();
        this["passive@"] = undefined;
        this["once@"] = false;
    }
    get passive() {
        const __ = this;
        return unwrap(__["passive@"]);
    }
    set passive(v) {
        const __ = this;
        __["passive@"] = v;
    }
    get once() {
        const __ = this;
        return __["once@"];
    }
    set once(v) {
        const __ = this;
        __["once@"] = v;
    }
}

export function AddEventListenerOptions_$reflection() {
    return class_type("Ranvier.Dom.AddEventListenerOptions", undefined, AddEventListenerOptions, EventListenerOptions_$reflection());
}

export function AddEventListenerOptions_$ctor() {
    return new AddEventListenerOptions();
}

/**
 * GetRootNodeOptions in the scoped DOM interface model.
 */
export class GetRootNodeOptions {
    constructor() {
        this["composed@"] = false;
    }
    get composed() {
        const __ = this;
        return __["composed@"];
    }
    set composed(v) {
        const __ = this;
        __["composed@"] = v;
    }
}

export function GetRootNodeOptions_$reflection() {
    return class_type("Ranvier.Dom.GetRootNodeOptions", undefined, GetRootNodeOptions);
}

export function GetRootNodeOptions_$ctor() {
    return new GetRootNodeOptions();
}

/**
 * GetHTMLOptions in the scoped DOM interface model.
 */
export class GetHTMLOptions {
    constructor() {
        this["serializableShadowRoots@"] = false;
        this["shadowRoots@"] = [];
    }
    get serializableShadowRoots() {
        const __ = this;
        return __["serializableShadowRoots@"];
    }
    set serializableShadowRoots(v) {
        const __ = this;
        __["serializableShadowRoots@"] = v;
    }
    get shadowRoots() {
        const __ = this;
        return __["shadowRoots@"];
    }
    set shadowRoots(v) {
        const __ = this;
        __["shadowRoots@"] = v;
    }
}

export function GetHTMLOptions_$reflection() {
    return class_type("Ranvier.Dom.GetHTMLOptions", undefined, GetHTMLOptions);
}

export function GetHTMLOptions_$ctor() {
    return new GetHTMLOptions();
}

/**
 * ElementCreationOptions in the scoped DOM interface model.
 */
export class ElementCreationOptions {
    constructor() {
    }
}

export function ElementCreationOptions_$reflection() {
    return class_type("Ranvier.Dom.ElementCreationOptions", undefined, ElementCreationOptions);
}

export function ElementCreationOptions_$ctor() {
    return new ElementCreationOptions();
}

/**
 * ImportNodeOptions in the scoped DOM interface model.
 */
export class ImportNodeOptions {
    constructor() {
        this["selfOnly@"] = false;
    }
    get selfOnly() {
        const __ = this;
        return __["selfOnly@"];
    }
    set selfOnly(v) {
        const __ = this;
        __["selfOnly@"] = v;
    }
}

export function ImportNodeOptions_$reflection() {
    return class_type("Ranvier.Dom.ImportNodeOptions", undefined, ImportNodeOptions);
}

export function ImportNodeOptions_$ctor() {
    return new ImportNodeOptions();
}

/**
 * AriaNotifyOptions in the scoped DOM interface model.
 */
export class AriaNotifyOptions {
    constructor() {
        this["priority@"] = undefined;
        this["interrupt@"] = undefined;
    }
    get priority() {
        const __ = this;
        return unwrap(__["priority@"]);
    }
    set priority(v) {
        const __ = this;
        __["priority@"] = v;
    }
    get interrupt() {
        const __ = this;
        return unwrap(__["interrupt@"]);
    }
    set interrupt(v) {
        const __ = this;
        __["interrupt@"] = v;
    }
}

export function AriaNotifyOptions_$reflection() {
    return class_type("Ranvier.Dom.AriaNotifyOptions", undefined, AriaNotifyOptions);
}

export function AriaNotifyOptions_$ctor() {
    return new AriaNotifyOptions();
}

/**
 * ShadowRootInit in the scoped DOM interface model.
 */
export class ShadowRootInit {
    constructor() {
        this["mode@"] = defaultOf();
        this["delegatesFocus@"] = false;
        this["clonable@"] = false;
        this["serializable@"] = false;
        this["slotAssignment@"] = "named";
    }
    get mode() {
        const __ = this;
        return __["mode@"];
    }
    set mode(v) {
        const __ = this;
        __["mode@"] = v;
    }
    get delegatesFocus() {
        const __ = this;
        return __["delegatesFocus@"];
    }
    set delegatesFocus(v) {
        const __ = this;
        __["delegatesFocus@"] = v;
    }
    get clonable() {
        const __ = this;
        return __["clonable@"];
    }
    set clonable(v) {
        const __ = this;
        __["clonable@"] = v;
    }
    get serializable() {
        const __ = this;
        return __["serializable@"];
    }
    set serializable(v) {
        const __ = this;
        __["serializable@"] = v;
    }
    get slotAssignment() {
        const __ = this;
        return __["slotAssignment@"];
    }
    set slotAssignment(v) {
        const __ = this;
        __["slotAssignment@"] = v;
    }
}

export function ShadowRootInit_$reflection() {
    return class_type("Ranvier.Dom.ShadowRootInit", undefined, ShadowRootInit);
}

export function ShadowRootInit_$ctor() {
    return new ShadowRootInit();
}

/**
 * CheckVisibilityOptions in the scoped DOM interface model.
 */
export class CheckVisibilityOptions {
    constructor() {
        this["opacityProperty@"] = false;
        this["visibilityProperty@"] = false;
        this["contentVisibilityAuto@"] = false;
        this["checkOpacity@"] = false;
        this["checkVisibilityCSS@"] = false;
    }
    get opacityProperty() {
        const __ = this;
        return __["opacityProperty@"];
    }
    set opacityProperty(v) {
        const __ = this;
        __["opacityProperty@"] = v;
    }
    get visibilityProperty() {
        const __ = this;
        return __["visibilityProperty@"];
    }
    set visibilityProperty(v) {
        const __ = this;
        __["visibilityProperty@"] = v;
    }
    get contentVisibilityAuto() {
        const __ = this;
        return __["contentVisibilityAuto@"];
    }
    set contentVisibilityAuto(v) {
        const __ = this;
        __["contentVisibilityAuto@"] = v;
    }
    get checkOpacity() {
        const __ = this;
        return __["checkOpacity@"];
    }
    set checkOpacity(v) {
        const __ = this;
        __["checkOpacity@"] = v;
    }
    get checkVisibilityCSS() {
        const __ = this;
        return __["checkVisibilityCSS@"];
    }
    set checkVisibilityCSS(v) {
        const __ = this;
        __["checkVisibilityCSS@"] = v;
    }
}

export function CheckVisibilityOptions_$reflection() {
    return class_type("Ranvier.Dom.CheckVisibilityOptions", undefined, CheckVisibilityOptions);
}

export function CheckVisibilityOptions_$ctor() {
    return new CheckVisibilityOptions();
}

/**
 * ScrollIntoViewOptions in the scoped DOM interface model.
 */
export class ScrollIntoViewOptions {
    constructor() {
        this["behavior@"] = "auto";
        this["block@"] = undefined;
        this["inline@"] = undefined;
    }
    get behavior() {
        const __ = this;
        return __["behavior@"];
    }
    set behavior(v) {
        const __ = this;
        __["behavior@"] = v;
    }
    get block() {
        const __ = this;
        return unwrap(__["block@"]);
    }
    set block(v) {
        const __ = this;
        __["block@"] = v;
    }
    get inline() {
        const __ = this;
        return unwrap(__["inline@"]);
    }
    set inline(v) {
        const __ = this;
        __["inline@"] = v;
    }
}

export function ScrollIntoViewOptions_$reflection() {
    return class_type("Ranvier.Dom.ScrollIntoViewOptions", undefined, ScrollIntoViewOptions);
}

export function ScrollIntoViewOptions_$ctor() {
    return new ScrollIntoViewOptions();
}

/**
 * ScrollToOptions in the scoped DOM interface model.
 */
export class ScrollToOptions {
    constructor() {
        this["behavior@"] = "auto";
        this["left@"] = undefined;
        this["top@"] = undefined;
    }
    get behavior() {
        const __ = this;
        return __["behavior@"];
    }
    set behavior(v) {
        const __ = this;
        __["behavior@"] = v;
    }
    get left() {
        const __ = this;
        return unwrap(__["left@"]);
    }
    set left(v) {
        const __ = this;
        __["left@"] = v;
    }
    get top() {
        const __ = this;
        return unwrap(__["top@"]);
    }
    set top(v) {
        const __ = this;
        __["top@"] = v;
    }
}

export function ScrollToOptions_$reflection() {
    return class_type("Ranvier.Dom.ScrollToOptions", undefined, ScrollToOptions);
}

export function ScrollToOptions_$ctor() {
    return new ScrollToOptions();
}

/**
 * ShowPopoverOptions in the scoped DOM interface model.
 */
export class ShowPopoverOptions {
    constructor() {
        this["source@"] = undefined;
    }
    get source() {
        const __ = this;
        return unwrap(__["source@"]);
    }
    set source(v) {
        const __ = this;
        __["source@"] = v;
    }
}

export function ShowPopoverOptions_$reflection() {
    return class_type("Ranvier.Dom.ShowPopoverOptions", undefined, ShowPopoverOptions);
}

export function ShowPopoverOptions_$ctor() {
    return new ShowPopoverOptions();
}

/**
 * TogglePopoverOptions in the scoped DOM interface model.
 */
export class TogglePopoverOptions {
    constructor() {
        this["source@"] = undefined;
        this["force@"] = undefined;
    }
    get source() {
        const __ = this;
        return unwrap(__["source@"]);
    }
    set source(v) {
        const __ = this;
        __["source@"] = v;
    }
    get force() {
        const __ = this;
        return unwrap(__["force@"]);
    }
    set force(v) {
        const __ = this;
        __["force@"] = v;
    }
}

export function TogglePopoverOptions_$reflection() {
    return class_type("Ranvier.Dom.TogglePopoverOptions", undefined, TogglePopoverOptions);
}

export function TogglePopoverOptions_$ctor() {
    return new TogglePopoverOptions();
}

/**
 * AssignedNodesOptions in the scoped DOM interface model.
 */
export class AssignedNodesOptions {
    constructor() {
        this["flatten@"] = false;
    }
    get flatten() {
        const __ = this;
        return __["flatten@"];
    }
    set flatten(v) {
        const __ = this;
        __["flatten@"] = v;
    }
}

export function AssignedNodesOptions_$reflection() {
    return class_type("Ranvier.Dom.AssignedNodesOptions", undefined, AssignedNodesOptions);
}

export function AssignedNodesOptions_$ctor() {
    return new AssignedNodesOptions();
}

/**
 * GetComposedRangesOptions in the scoped DOM interface model.
 */
export class GetComposedRangesOptions {
    constructor() {
        this["shadowRoots@"] = [];
    }
    get shadowRoots() {
        const __ = this;
        return __["shadowRoots@"];
    }
    set shadowRoots(v) {
        const __ = this;
        __["shadowRoots@"] = v;
    }
}

export function GetComposedRangesOptions_$reflection() {
    return class_type("Ranvier.Dom.GetComposedRangesOptions", undefined, GetComposedRangesOptions);
}

export function GetComposedRangesOptions_$ctor() {
    return new GetComposedRangesOptions();
}

/**
 * DOMMatrixInit in the scoped DOM interface model.
 */
export class DOMMatrixInit {
    constructor() {
        this["a@"] = undefined;
        this["b@"] = undefined;
        this["c@"] = undefined;
        this["d@"] = undefined;
        this["e@"] = undefined;
        this["f@"] = undefined;
        this["m11@"] = undefined;
        this["m12@"] = undefined;
        this["m21@"] = undefined;
        this["m22@"] = undefined;
        this["m41@"] = undefined;
        this["m42@"] = undefined;
        this["m13@"] = 0;
        this["m14@"] = 0;
        this["m23@"] = 0;
        this["m24@"] = 0;
        this["m31@"] = 0;
        this["m32@"] = 0;
        this["m33@"] = 1;
        this["m34@"] = 0;
        this["m43@"] = 0;
        this["m44@"] = 1;
        this["is2D@"] = undefined;
    }
    get a() {
        const __ = this;
        return unwrap(__["a@"]);
    }
    set a(v) {
        const __ = this;
        __["a@"] = v;
    }
    get b() {
        const __ = this;
        return unwrap(__["b@"]);
    }
    set b(v) {
        const __ = this;
        __["b@"] = v;
    }
    get c() {
        const __ = this;
        return unwrap(__["c@"]);
    }
    set c(v) {
        const __ = this;
        __["c@"] = v;
    }
    get d() {
        const __ = this;
        return unwrap(__["d@"]);
    }
    set d(v) {
        const __ = this;
        __["d@"] = v;
    }
    get e() {
        const __ = this;
        return unwrap(__["e@"]);
    }
    set e(v) {
        const __ = this;
        __["e@"] = v;
    }
    get f() {
        const __ = this;
        return unwrap(__["f@"]);
    }
    set f(v) {
        const __ = this;
        __["f@"] = v;
    }
    get m11() {
        const __ = this;
        return unwrap(__["m11@"]);
    }
    set m11(v) {
        const __ = this;
        __["m11@"] = v;
    }
    get m12() {
        const __ = this;
        return unwrap(__["m12@"]);
    }
    set m12(v) {
        const __ = this;
        __["m12@"] = v;
    }
    get m21() {
        const __ = this;
        return unwrap(__["m21@"]);
    }
    set m21(v) {
        const __ = this;
        __["m21@"] = v;
    }
    get m22() {
        const __ = this;
        return unwrap(__["m22@"]);
    }
    set m22(v) {
        const __ = this;
        __["m22@"] = v;
    }
    get m41() {
        const __ = this;
        return unwrap(__["m41@"]);
    }
    set m41(v) {
        const __ = this;
        __["m41@"] = v;
    }
    get m42() {
        const __ = this;
        return unwrap(__["m42@"]);
    }
    set m42(v) {
        const __ = this;
        __["m42@"] = v;
    }
    get m13() {
        const __ = this;
        return __["m13@"];
    }
    set m13(v) {
        const __ = this;
        __["m13@"] = v;
    }
    get m14() {
        const __ = this;
        return __["m14@"];
    }
    set m14(v) {
        const __ = this;
        __["m14@"] = v;
    }
    get m23() {
        const __ = this;
        return __["m23@"];
    }
    set m23(v) {
        const __ = this;
        __["m23@"] = v;
    }
    get m24() {
        const __ = this;
        return __["m24@"];
    }
    set m24(v) {
        const __ = this;
        __["m24@"] = v;
    }
    get m31() {
        const __ = this;
        return __["m31@"];
    }
    set m31(v) {
        const __ = this;
        __["m31@"] = v;
    }
    get m32() {
        const __ = this;
        return __["m32@"];
    }
    set m32(v) {
        const __ = this;
        __["m32@"] = v;
    }
    get m33() {
        const __ = this;
        return __["m33@"];
    }
    set m33(v) {
        const __ = this;
        __["m33@"] = v;
    }
    get m34() {
        const __ = this;
        return __["m34@"];
    }
    set m34(v) {
        const __ = this;
        __["m34@"] = v;
    }
    get m43() {
        const __ = this;
        return __["m43@"];
    }
    set m43(v) {
        const __ = this;
        __["m43@"] = v;
    }
    get m44() {
        const __ = this;
        return __["m44@"];
    }
    set m44(v) {
        const __ = this;
        __["m44@"] = v;
    }
    get is2D() {
        const __ = this;
        return unwrap(__["is2D@"]);
    }
    set is2D(v) {
        const __ = this;
        __["is2D@"] = v;
    }
}

export function DOMMatrixInit_$reflection() {
    return class_type("Ranvier.Dom.DOMMatrixInit", undefined, DOMMatrixInit);
}

export function DOMMatrixInit_$ctor() {
    return new DOMMatrixInit();
}

/**
 * DOMPointInit in the scoped DOM interface model.
 */
export class DOMPointInit {
    constructor() {
        this["x@"] = 0;
        this["y@"] = 0;
        this["z@"] = 0;
        this["w@"] = 1;
    }
    get x() {
        const __ = this;
        return __["x@"];
    }
    set x(v) {
        const __ = this;
        __["x@"] = v;
    }
    get y() {
        const __ = this;
        return __["y@"];
    }
    set y(v) {
        const __ = this;
        __["y@"] = v;
    }
    get z() {
        const __ = this;
        return __["z@"];
    }
    set z(v) {
        const __ = this;
        __["z@"] = v;
    }
    get w() {
        const __ = this;
        return __["w@"];
    }
    set w(v) {
        const __ = this;
        __["w@"] = v;
    }
}

export function DOMPointInit_$reflection() {
    return class_type("Ranvier.Dom.DOMPointInit", undefined, DOMPointInit);
}

export function DOMPointInit_$ctor() {
    return new DOMPointInit();
}

/**
 * FocusOptions in the scoped DOM interface model.
 */
export class FocusOptions {
    constructor() {
        this["preventScroll@"] = false;
        this["focusVisible@"] = undefined;
    }
    get preventScroll() {
        const __ = this;
        return __["preventScroll@"];
    }
    set preventScroll(v) {
        const __ = this;
        __["preventScroll@"] = v;
    }
    get focusVisible() {
        const __ = this;
        return unwrap(__["focusVisible@"]);
    }
    set focusVisible(v) {
        const __ = this;
        __["focusVisible@"] = v;
    }
}

export function FocusOptions_$reflection() {
    return class_type("Ranvier.Dom.FocusOptions", undefined, FocusOptions);
}

export function FocusOptions_$ctor() {
    return new FocusOptions();
}

function BrowserUnionAdapters_boxDOMStringOrElementCreationOptions(value) {
    if (value.tag === 1) {
        return value.fields[0];
    }
    else {
        return value.fields[0];
    }
}

function BrowserUnionAdapters_boxBooleanOrImportNodeOptions(value) {
    if (value.tag === 1) {
        return value.fields[0];
    }
    else {
        return value.fields[0];
    }
}

function BrowserUnionAdapters_boxBooleanOrScrollIntoViewOptions(value) {
    if (value.tag === 1) {
        return value.fields[0];
    }
    else {
        return value.fields[0];
    }
}

function BrowserUnionAdapters_boxBooleanOrUnrestrictedDoubleOrDOMString(value) {
    switch (value.tag) {
        case 1:
            return value.fields[0];
        case 2:
            return value.fields[0];
        default:
            return value.fields[0];
    }
}

function BrowserUnionAdapters_unboxBooleanOrUnrestrictedDoubleOrDOMString(value) {
    if (typeof value === "boolean") {
        return new BooleanOrUnrestrictedDoubleOrDOMString(/* Boolean */ 0, [value]);
    }
    else if (typeof value === "number") {
        return new BooleanOrUnrestrictedDoubleOrDOMString(/* UnrestrictedDouble */ 1, [value]);
    }
    else if (typeof value === "string") {
        return new BooleanOrUnrestrictedDoubleOrDOMString(/* DOMString */ 2, [value]);
    }
    else {
        throw new Exception("Unexpected DOM union result");
    }
}

function BrowserUnionAdapters_boxTogglePopoverOptionsOrBoolean(value) {
    if (value.tag === 1) {
        return value.fields[0];
    }
    else {
        return value.fields[0];
    }
}

function BrowserUnionAdapters_boxRadioNodeListOrElement(value) {
    if (value.tag === 1) {
        return value.fields[0];
    }
    else {
        return value.fields[0];
    }
}

function BrowserUnionAdapters_unboxRadioNodeListOrElement(value) {
    if (Object.prototype.toString.call(value) === '[object RadioNodeList]') {
        return new RadioNodeListOrElement(/* RadioNodeList */ 0, [value]);
    }
    else if (value != null && value.nodeType === 1) {
        return new RadioNodeListOrElement(/* Element */ 1, [value]);
    }
    else {
        throw new Exception("Unexpected DOM union result");
    }
}

function BrowserUnionAdapters_boxHTMLOptionElementOrHTMLOptGroupElement(value) {
    if (value.tag === 1) {
        return value.fields[0];
    }
    else {
        return value.fields[0];
    }
}

function BrowserUnionAdapters_boxHTMLElementOrLong(value) {
    if (value.tag === 1) {
        return value.fields[0];
    }
    else {
        return value.fields[0];
    }
}

function BrowserUnionAdapters_boxElementOrText(value) {
    if (value.tag === 1) {
        return value.fields[0];
    }
    else {
        return value.fields[0];
    }
}

function BrowserUnionAdapters_boxDoubleOrCSSNumericValue(value) {
    if (Operators_IsNull(value)) {
        Operators_NullArg("value");
    }
    if (value.tag === 1) {
        const value_2 = value.fields[0];
        if (Operators_IsNull(value_2)) {
            Operators_NullArg("value");
        }
        if (!(((v)=>{if(v==null)return false;try{globalThis.CSSNumericValue.prototype.type.call(v);return true;}catch{return false;}})(value_2))) {
            throw new ArgumentException("Expected a CSSNumericValue.", "value", defaultOf());
        }
        return value_2;
    }
    else {
        return value.fields[0];
    }
}

function BrowserUnionAdapters_unboxDoubleOrCSSNumericValue(value) {
    if (typeof value === "number") {
        return new DoubleOrCSSNumericValue(/* Double */ 0, [value]);
    }
    else if (((v)=>{if(v==null)return false;try{globalThis.CSSNumericValue.prototype.type.call(v);return true;}catch{return false;}})(value)) {
        return new DoubleOrCSSNumericValue(/* CSSNumericValue */ 1, [value]);
    }
    else {
        throw new Exception("Unexpected DOM union result");
    }
}

function BrowserUnionAdapters_boxCSSNumericValueOrDOMStringOrCSSOMKeywordValue(value) {
    if (Operators_IsNull(value)) {
        Operators_NullArg("value");
    }
    switch (value.tag) {
        case 1:
            return value.fields[0];
        case 2: {
            const value_3 = value.fields[0];
            if (Operators_IsNull(value_3)) {
                Operators_NullArg("value");
            }
            if (!(((v)=>{if(v==null)return false;try{Object.getOwnPropertyDescriptor(globalThis.CSSKeywordValue.prototype,'value').get.call(v);return true;}catch{return false;}})(value_3))) {
                throw new ArgumentException("Expected a CSSKeywordValue.", "value", defaultOf());
            }
            return value_3;
        }
        default: {
            const value_1 = value.fields[0];
            if (Operators_IsNull(value_1)) {
                Operators_NullArg("value");
            }
            if (!(((v)=>{if(v==null)return false;try{globalThis.CSSNumericValue.prototype.type.call(v);return true;}catch{return false;}})(value_1))) {
                throw new ArgumentException("Expected a CSSNumericValue.", "value", defaultOf());
            }
            return value_1;
        }
    }
}

function BrowserUnionAdapters_unboxCSSNumericValueOrDOMStringOrCSSOMKeywordValue(value) {
    if (((v)=>{if(v==null)return false;try{globalThis.CSSNumericValue.prototype.type.call(v);return true;}catch{return false;}})(value)) {
        return new CSSNumericValueOrDOMStringOrCSSOMKeywordValue(/* CSSNumericValue */ 0, [value]);
    }
    else if (typeof value === "string") {
        const arm_3 = value;
        return new CSSNumericValueOrDOMStringOrCSSOMKeywordValue(/* DOMString */ 1, [arm_3]);
    }
    else if (((v)=>{if(v==null)return false;try{Object.getOwnPropertyDescriptor(globalThis.CSSKeywordValue.prototype,'value').get.call(v);return true;}catch{return false;}})(value)) {
        return new CSSNumericValueOrDOMStringOrCSSOMKeywordValue(/* CSSOMKeywordValue */ 2, [value]);
    }
    else {
        throw new Exception("Unexpected DOM union result");
    }
}

function BrowserUnionAdapters_boxUSVStringOrCSSOMVariableReferenceValue(value) {
    if (Operators_IsNull(value)) {
        Operators_NullArg("value");
    }
    if (value.tag === 1) {
        const value_2 = value.fields[0];
        if (Operators_IsNull(value_2)) {
            Operators_NullArg("value");
        }
        if (!(((v)=>{if(v==null)return false;try{Object.getOwnPropertyDescriptor(globalThis.CSSVariableReferenceValue.prototype,'variable').get.call(v);return true;}catch{return false;} })(value_2))) {
            throw new ArgumentException("Expected a CSSVariableReferenceValue.", "value", defaultOf());
        }
        return value_2;
    }
    else {
        return ((v)=>String(v==null?'null':v).toWellFormed())(value.fields[0]);
    }
}

function BrowserUnionAdapters_unboxUSVStringOrCSSOMVariableReferenceValue(value) {
    if (typeof value === "string") {
        const arm_1 = value;
        return new USVStringOrCSSOMVariableReferenceValue(/* USVString */ 0, [arm_1]);
    }
    else if (((v)=>{if(v==null)return false;try{Object.getOwnPropertyDescriptor(globalThis.CSSVariableReferenceValue.prototype,'variable').get.call(v);return true;}catch{return false;} })(value)) {
        return new USVStringOrCSSOMVariableReferenceValue(/* CSSOMVariableReferenceValue */ 1, [value]);
    }
    else {
        throw new Exception("Unexpected DOM union result");
    }
}

function BrowserUnionAdapters_boxUndefinedOrCSSStyleValue(value) {
    if (value.tag === 1) {
        return value.fields[0];
    }
    else {
        return undefined;
    }
}

function BrowserUnionAdapters_unboxUndefinedOrCSSStyleValue(value) {
    if (value === undefined) {
        return new UndefinedOrCSSStyleValue(/* Undefined */ 0, [undefined]);
    }
    else if (((v)=>{if(v==null)return false;try{globalThis.CSSStyleValue.prototype.toString.call(v);return true;}catch{return false;}})(value)) {
        return new UndefinedOrCSSStyleValue(/* CSSStyleValue */ 1, [value]);
    }
    else {
        throw new Exception("Unexpected DOM union result");
    }
}

function BrowserUnionAdapters_boxCSSStyleValueOrUSVString(value) {
    if (Operators_IsNull(value)) {
        Operators_NullArg("value");
    }
    if (value.tag === 1) {
        return ((v)=>String(v==null?'null':v).toWellFormed())(value.fields[0]);
    }
    else {
        const value_1 = value.fields[0];
        if (Operators_IsNull(value_1)) {
            Operators_NullArg("value");
        }
        if (!(((v)=>{if(v==null)return false;try{globalThis.CSSStyleValue.prototype.toString.call(v);return true;}catch{return false;}})(value_1))) {
            throw new ArgumentException("Expected a CSSStyleValue.", "value", defaultOf());
        }
        return value_1;
    }
}

function BrowserUnionAdapters_boxShadowRootOrGetComposedRangesOptions(value) {
    if (value.tag === 1) {
        return value.fields[0];
    }
    else {
        return value.fields[0];
    }
}

function BrowserUnionAdapters_boxNodeOrDOMString(value) {
    if (value.tag === 1) {
        return value.fields[0];
    }
    else {
        return value.fields[0];
    }
}

export function Ranvier_Dom_Document__Document_createElement_Z721C83C5(this$, localName) {
    return this$.createElement(localName, undefined);
}

export function Ranvier_Dom_Document__Document_createElement_Z384F8060(this$, localName, options) {
    return this$.createElement(localName, options);
}

export function Ranvier_Dom_Document__Document_createElement_1F0AA12A(this$, localName, options) {
    return this$.createElement(localName, options);
}

export function Ranvier_Dom_Document__Document_createElementNS_Z384F8060(this$, namespaceURI, qualifiedName) {
    return this$.createElementNS(namespaceURI, qualifiedName, undefined);
}

export function Ranvier_Dom_Document__Document_createElementNS_30230F9B(this$, namespaceURI, qualifiedName, options) {
    return this$.createElementNS(namespaceURI, qualifiedName, options);
}

export function Ranvier_Dom_Document__Document_createElementNS_Z17662EEF(this$, namespaceURI, qualifiedName, options) {
    return this$.createElementNS(namespaceURI, qualifiedName, options);
}

export function Ranvier_Dom_Document__Document_importNode_Z77A9334A(this$, node) {
    return this$.importNode(node, undefined);
}

export function Ranvier_Dom_Document__Document_importNode_7373519C(this$, node, options) {
    return this$.importNode(node, options);
}

export function Ranvier_Dom_Document__Document_importNode_Z2A26D3F3(this$, node, options) {
    return this$.importNode(node, options);
}

export function Ranvier_Dom_Element__Element_scrollIntoView(this$) {
    this$.scrollIntoView(undefined);
}

export function Ranvier_Dom_Element__Element_scrollIntoView_Z1FBCCD16(this$, arg) {
    this$.scrollIntoView(arg);
}

export function Ranvier_Dom_Element__Element_scrollIntoView_Z252A09C6(this$, arg) {
    this$.scrollIntoView(arg);
}

export function Ranvier_Dom_HTMLElement__HTMLElement_get_hidden(this$) {
    const value = this$.hidden;
    if (Operators_IsNull(value)) {
        return undefined;
    }
    else {
        return BrowserUnionAdapters_unboxBooleanOrUnrestrictedDoubleOrDOMString(value);
    }
}

export function Ranvier_Dom_HTMLElement__HTMLElement_set_hidden_Z35ABEB6F(this$, value) {
    let option_1;
    this$.hidden = defaultArg((option_1 = value, (option_1 != null) ? some(BrowserUnionAdapters_boxBooleanOrUnrestrictedDoubleOrDOMString(option_1)) : undefined), defaultOf());
}

export function Ranvier_Dom_HTMLElement__HTMLElement_togglePopover(this$) {
    return this$.togglePopover(undefined);
}

export function Ranvier_Dom_HTMLElement__HTMLElement_togglePopover_Z570B958B(this$, options) {
    return this$.togglePopover(options);
}

export function Ranvier_Dom_HTMLElement__HTMLElement_togglePopover_Z1FBCCD16(this$, options) {
    return this$.togglePopover(options);
}

export function Ranvier_Dom_HTMLFormElement__HTMLFormElement_namedItem_Z721C83C5(this$, name) {
    const value = this$[name];
    if (Operators_IsNull(value)) {
        return undefined;
    }
    else {
        return BrowserUnionAdapters_unboxRadioNodeListOrElement(value);
    }
}

export function Ranvier_Dom_HTMLSelectElement__HTMLSelectElement_add_Z4588504A(this$, element) {
    this$.add(element, undefined);
}

export function Ranvier_Dom_HTMLSelectElement__HTMLSelectElement_add_7D375783(this$, element, before) {
    this$.add(element, before);
}

export function Ranvier_Dom_HTMLSelectElement__HTMLSelectElement_add_Z5B2FFFD6(this$, element, before) {
    this$.add(element, before);
}

export function Ranvier_Dom_HTMLSelectElement__HTMLSelectElement_add_5B00A961(this$, element) {
    this$.add(element, undefined);
}

export function Ranvier_Dom_HTMLSelectElement__HTMLSelectElement_add_Z30B0DB8C(this$, element, before) {
    this$.add(element, before);
}

export function Ranvier_Dom_HTMLSelectElement__HTMLSelectElement_add_16A873DD(this$, element, before) {
    this$.add(element, before);
}

export function Ranvier_Dom_HTMLSlotElement__HTMLSlotElement_assign_349BC103(this$, ...nodes) {
    this$.assign(...map(BrowserUnionAdapters_boxElementOrText, nodes));
}

export function Ranvier_Dom_CSSRotate__CSSRotate_get_x(this$) {
    return BrowserUnionAdapters_unboxDoubleOrCSSNumericValue(this$.x);
}

export function Ranvier_Dom_CSSRotate__CSSRotate_set_x_Z50295C3(this$, value) {
    this$.x = BrowserUnionAdapters_boxDoubleOrCSSNumericValue(value);
}

export function Ranvier_Dom_CSSRotate__CSSRotate_get_y(this$) {
    return BrowserUnionAdapters_unboxDoubleOrCSSNumericValue(this$.y);
}

export function Ranvier_Dom_CSSRotate__CSSRotate_set_y_Z50295C3(this$, value) {
    this$.y = BrowserUnionAdapters_boxDoubleOrCSSNumericValue(value);
}

export function Ranvier_Dom_CSSRotate__CSSRotate_get_z(this$) {
    return BrowserUnionAdapters_unboxDoubleOrCSSNumericValue(this$.z);
}

export function Ranvier_Dom_CSSRotate__CSSRotate_set_z_Z50295C3(this$, value) {
    this$.z = BrowserUnionAdapters_boxDoubleOrCSSNumericValue(value);
}

export function Ranvier_Dom_CSSScale__CSSScale_get_x(this$) {
    return BrowserUnionAdapters_unboxDoubleOrCSSNumericValue(this$.x);
}

export function Ranvier_Dom_CSSScale__CSSScale_set_x_Z50295C3(this$, value) {
    this$.x = BrowserUnionAdapters_boxDoubleOrCSSNumericValue(value);
}

export function Ranvier_Dom_CSSScale__CSSScale_get_y(this$) {
    return BrowserUnionAdapters_unboxDoubleOrCSSNumericValue(this$.y);
}

export function Ranvier_Dom_CSSScale__CSSScale_set_y_Z50295C3(this$, value) {
    this$.y = BrowserUnionAdapters_boxDoubleOrCSSNumericValue(value);
}

export function Ranvier_Dom_CSSScale__CSSScale_get_z(this$) {
    return BrowserUnionAdapters_unboxDoubleOrCSSNumericValue(this$.z);
}

export function Ranvier_Dom_CSSScale__CSSScale_set_z_Z50295C3(this$, value) {
    this$.z = BrowserUnionAdapters_boxDoubleOrCSSNumericValue(value);
}

export function Ranvier_Dom_CSSPerspective__CSSPerspective_get_length(this$) {
    return BrowserUnionAdapters_unboxCSSNumericValueOrDOMStringOrCSSOMKeywordValue(this$.length);
}

export function Ranvier_Dom_CSSPerspective__CSSPerspective_set_length_3279C475(this$, value) {
    this$.length = BrowserUnionAdapters_boxCSSNumericValueOrDOMStringOrCSSOMKeywordValue(value);
}

export function Ranvier_Dom_CSSUnparsedValue__CSSUnparsedValue_item_Z6EF827D7(this$, index) {
    const value = this$[index];
    if (Operators_IsNull(value)) {
        return undefined;
    }
    else {
        return BrowserUnionAdapters_unboxUSVStringOrCSSOMVariableReferenceValue(value);
    }
}

export function Ranvier_Dom_CSSUnparsedValue__CSSUnparsedValue_setItem_3FE1A172(this$, index, val) {
    ((v,i,s)=>{if(i===4294967295)throw new RangeError('Index out of range.');v[i]=s;})(this$,index,(((v)=>String(v==null?'null':v).toWellFormed())(val)));
}

export function Ranvier_Dom_CSSUnparsedValue__CSSUnparsedValue_setItem_35D025DA(this$, index, val) {
    if (Operators_IsNull(val)) {
        Operators_NullArg("val");
    }
    if (!(((v)=>{if(v==null)return false;try{Object.getOwnPropertyDescriptor(globalThis.CSSVariableReferenceValue.prototype,'variable').get.call(v);return true;}catch{return false;} })(val))) {
        throw new ArgumentException("Expected a CSSVariableReferenceValue.", "val", defaultOf());
    }
    ((v,i,s)=>{if(i===4294967295)throw new RangeError('Index out of range.');v[i]=s;})(this$,index,val);
}

export function Ranvier_Dom_StylePropertyMapReadOnly__StylePropertyMapReadOnly_get_Z721C83C5(this$, property) {
    return BrowserUnionAdapters_unboxUndefinedOrCSSStyleValue(this$.get(property));
}

export function Ranvier_Dom_StylePropertyMap__StylePropertyMap_set_Z2399179F(this$, property, ...values) {
    this$.set(property, ...map(BrowserUnionAdapters_boxCSSStyleValueOrUSVString, values));
}

export function Ranvier_Dom_StylePropertyMap__StylePropertyMap_append_Z2399179F(this$, property, ...values) {
    this$.append(property, ...map(BrowserUnionAdapters_boxCSSStyleValueOrUSVString, values));
}

export function Ranvier_Dom_DOMSelection__DOMSelection_getComposedRanges_55C7A59D(this$, ...shadowRoots) {
    return this$.getComposedRanges(undefined, ...shadowRoots);
}

export function Ranvier_Dom_DOMSelection__DOMSelection_getComposedRanges_7D0AC64B(this$, options, ...shadowRoots) {
    return this$.getComposedRanges(options, ...shadowRoots);
}

export function Ranvier_Dom_DOMSelection__DOMSelection_getComposedRanges_318D446D(this$, options, ...shadowRoots) {
    return this$.getComposedRanges(options, ...shadowRoots);
}

export function Ranvier_Dom_ChildNode__ChildNode_before_6ECD82B3(this$, ...nodes) {
    this$.before(...map(BrowserUnionAdapters_boxNodeOrDOMString, nodes));
}

export function Ranvier_Dom_ChildNode__ChildNode_after_6ECD82B3(this$, ...nodes) {
    this$.after(...map(BrowserUnionAdapters_boxNodeOrDOMString, nodes));
}

export function Ranvier_Dom_ChildNode__ChildNode_replaceWith_6ECD82B3(this$, ...nodes) {
    this$.replaceWith(...map(BrowserUnionAdapters_boxNodeOrDOMString, nodes));
}

export function Ranvier_Dom_ParentNode__ParentNode_prepend_6ECD82B3(this$, ...nodes) {
    this$.prepend(...map(BrowserUnionAdapters_boxNodeOrDOMString, nodes));
}

export function Ranvier_Dom_ParentNode__ParentNode_append_6ECD82B3(this$, ...nodes) {
    this$.append(...map(BrowserUnionAdapters_boxNodeOrDOMString, nodes));
}

export function Ranvier_Dom_ParentNode__ParentNode_replaceChildren_6ECD82B3(this$, ...nodes) {
    this$.replaceChildren(...map(BrowserUnionAdapters_boxNodeOrDOMString, nodes));
}

