
import { FSharpException, Record, Union } from "../../../../../../../../fable_modules/fable-library-js.5.24.0/Types.js";
import { lambda_type, class_type, option_type, array_type, float64_type, bool_type, record_type, string_type, union_type } from "../../../../../../../../fable_modules/fable-library-js.5.24.0/Reflection.js";
import { cancel, isCancellationRequested, createCancellationToken, startAsTask } from "../../../../../../../../fable_modules/fable-library-js.5.24.0/Async.js";
import { OperationCanceledException, singleton } from "../../../../../../../../fable_modules/fable-library-js.5.24.0/AsyncBuilder.js";
import { clear, curry2, safeHash, disposeSafe, getEnumerator, equals, defaultOf } from "../../../../../../../../fable_modules/fable-library-js.5.24.0/Util.js";
import { Operators_Lock, Operators_NullArg, Operators_IsNull } from "../../../../../../../../fable_modules/fable-library-js.5.24.0/FSharp.Core.js";
import { forAll, exists } from "../../../../../../../../fable_modules/fable-library-js.5.24.0/Seq.js";
import { isLetterOrDigit, isWhiteSpace, isControl } from "../../../../../../../../fable_modules/fable-library-js.5.24.0/Char.js";
import { InvalidOperationException, ArgumentException } from "../../../../../../../../fable_modules/fable-library-js.5.24.0/System.js";
import { split } from "../../../../../../../../fable_modules/fable-library-js.5.24.0/String.js";
import { removeInPlace, collect, choose, map, copy, item as item_1, equalsWith } from "../../../../../../../../fable_modules/fable-library-js.5.24.0/Array.js";
import { isInfinity } from "../../../../../../../../fable_modules/fable-library-js.5.24.0/Double.js";
import { Array_distinct } from "../../../../../../../../fable_modules/fable-library-js.5.24.0/Seq2.js";
import { task } from "../../../../../../../../fable_modules/fable-library-js.5.24.0/TaskBuilder.js";

/**
 * The runtime observation domain; format domains describe support queries.
 */
export class CapabilityCategory extends Union {
    constructor(tag, fields) {
        super();
        this.tag = tag;
        this.fields = fields;
    }
    cases() {
        return ["Abi", "RendererSetting", "ImageFormat", "FontFormat", "PlatformFacility"];
    }
    static Abi = new CapabilityCategory(0, []);
    static RendererSetting = new CapabilityCategory(1, []);
    static ImageFormat = new CapabilityCategory(2, []);
    static FontFormat = new CapabilityCategory(3, []);
    static PlatformFacility = new CapabilityCategory(4, []);
}

export function CapabilityCategory_$reflection() {
    return union_type("Ranvier.Dom.CapabilityCategory", [], CapabilityCategory, () => [[], [], [], [], []]);
}

/**
 * A category and normalized nonempty name; use Capabilities.key to validate external input.
 */
export class CapabilityKey extends Record {
    constructor(Category, Name) {
        super();
        this.Category = Category;
        this.Name = Name;
    }
}

export function CapabilityKey_$reflection() {
    return record_type("Ranvier.Dom.CapabilityKey", [], CapabilityKey, () => [["Category", CapabilityCategory_$reflection()], ["Name", string_type]]);
}

/**
 * An independently observed fact, with Unknown for unobservable or contradictory evidence.
 */
export class CapabilityState extends Union {
    constructor(tag, fields) {
        super();
        this.tag = tag;
        this.fields = fields;
    }
    cases() {
        return ["Yes", "No", "Unknown"];
    }
    static Yes = new CapabilityState(0, []);
    static No = new CapabilityState(1, []);
    static Unknown = new CapabilityState(2, []);
}

export function CapabilityState_$reflection() {
    return union_type("Ranvier.Dom.CapabilityState", [], CapabilityState, () => [[], [], []]);
}

/**
 * A fact and the reason supported by its provider's evidence.
 */
export class CapabilityFact extends Record {
    constructor(State, Reason) {
        super();
        this.State = State;
        this.Reason = Reason;
    }
}

export function CapabilityFact_$reflection() {
    return record_type("Ranvier.Dom.CapabilityFact", [], CapabilityFact, () => [["State", CapabilityState_$reflection()], ["Reason", string_type]]);
}

/**
 * A current observed value; numbers must be finite and name arrays are copied.
 */
export class CapabilityValue extends Union {
    constructor(tag, fields) {
        super();
        this.tag = tag;
        this.fields = fields;
    }
    cases() {
        return ["Boolean", "Number", "Text", "Names"];
    }
}

export function CapabilityValue_$reflection() {
    return union_type("Ranvier.Dom.CapabilityValue", [], CapabilityValue, () => [[["Item", bool_type]], [["Item", float64_type]], [["Item", string_type]], [["Item", array_type(string_type)]]]);
}

/**
 * Independent runtime facts with provider identity and observation mechanism.
 */
export class CapabilityObservation extends Record {
    constructor(Key, Exported, Implemented, Enabled, Permitted, Available, Value, ProviderId, Mechanism, Scope) {
        super();
        this.Key = Key;
        this.Exported = Exported;
        this.Implemented = Implemented;
        this.Enabled = Enabled;
        this.Permitted = Permitted;
        this.Available = Available;
        this.Value = Value;
        this.ProviderId = ProviderId;
        this.Mechanism = Mechanism;
        this.Scope = Scope;
    }
}

export function CapabilityObservation_$reflection() {
    return record_type("Ranvier.Dom.CapabilityObservation", [], CapabilityObservation, () => [["Key", CapabilityKey_$reflection()], ["Exported", CapabilityFact_$reflection()], ["Implemented", CapabilityFact_$reflection()], ["Enabled", CapabilityFact_$reflection()], ["Permitted", CapabilityFact_$reflection()], ["Available", CapabilityFact_$reflection()], ["Value", option_type(CapabilityValue_$reflection())], ["ProviderId", string_type], ["Mechanism", string_type], ["Scope", string_type]]);
}

/**
 * A combined snapshot retaining every contribution, including conflicting and unknown facts.
 */
export class CapabilityReport extends Record {
    constructor(Key, Exported, Implemented, Enabled, Permitted, Available, Value, Observations) {
        super();
        this.Key = Key;
        this.Exported = Exported;
        this.Implemented = Implemented;
        this.Enabled = Enabled;
        this.Permitted = Permitted;
        this.Available = Available;
        this.Value = Value;
        this.Observations = Observations;
    }
}

export function CapabilityReport_$reflection() {
    return record_type("Ranvier.Dom.CapabilityReport", [], CapabilityReport, () => [["Key", CapabilityKey_$reflection()], ["Exported", CapabilityFact_$reflection()], ["Implemented", CapabilityFact_$reflection()], ["Enabled", CapabilityFact_$reflection()], ["Permitted", CapabilityFact_$reflection()], ["Available", CapabilityFact_$reflection()], ["Value", option_type(CapabilityValue_$reflection())], ["Observations", array_type(CapabilityObservation_$reflection())]]);
}

/**
 * A named provider; callbacks start on the caller's thread and may complete asynchronously.
 */
export class CapabilityProvider extends Record {
    constructor(Id, KnownKeys, Observe) {
        super();
        this.Id = Id;
        this.KnownKeys = KnownKeys;
        this.Observe = Observe;
    }
}

export function CapabilityProvider_$reflection() {
    return record_type("Ranvier.Dom.CapabilityProvider", [], CapabilityProvider, () => [["Id", string_type], ["KnownKeys", array_type(CapabilityKey_$reflection())], ["Observe", lambda_type(CapabilityKey_$reflection(), lambda_type(class_type("System.Threading.CancellationToken"), class_type("System.Threading.Tasks.Task`1", [option_type(CapabilityObservation_$reflection())])))]]);
}

/**
 * A context or affinity failure that must propagate instead of becoming unknown provider evidence.
 */
export class CapabilityContextException extends FSharpException {
    constructor(detail) {
        super();
        this.detail = detail;
    }
    "System.Exception.get_Message"() {
        const this$ = this;
        return this$.detail;
    }
}

export function CapabilityContextException_$reflection() {
    return class_type("Ranvier.Dom.CapabilityContextException", undefined, CapabilityContextException, class_type("System.Exception"));
}

/**
 * Publishes at most one result, error or cancellation.
 */
export class CapabilityTasks_Completion$1 {
    constructor() {
        this.completed = false;
        this.resolve = ((_arg) => {
        });
        this.reject = ((_arg_1) => {
        });
        this.value = (new Promise((yes, no) => {
            this.resolve = yes;
            this.reject = no;
        }));
    }
}

export function CapabilityTasks_Completion$1_$reflection(gen0) {
    return class_type("Ranvier.Dom.CapabilityTasks.Completion`1", [gen0], CapabilityTasks_Completion$1);
}

export function CapabilityTasks_Completion$1_$ctor() {
    return new CapabilityTasks_Completion$1();
}

export function CapabilityTasks_Completion$1__get_Task(_) {
    return _.value;
}

export function CapabilityTasks_Completion$1__TrySetResult_2B595(_, item) {
    return CapabilityTasks_Completion$1__finish_3A5B6456(_, () => {
        _.resolve(item);
    });
}

export function CapabilityTasks_Completion$1__TrySetException_229D3F39(_, error) {
    return CapabilityTasks_Completion$1__finish_3A5B6456(_, () => {
        _.reject(error);
    });
}

export function CapabilityTasks_Completion$1__TrySetCanceled_Z211DAE3E(_, token) {
    return CapabilityTasks_Completion$1__finish_3A5B6456(_, () => {
        const cancelled = startAsTask(singleton.Delay(() => singleton.Return(defaultOf())), undefined, token);
        cancelled.catch(_.reject);
    });
}

export function CapabilityTasks_Completion$1__finish_3A5B6456(this$, action) {
    if (this$.completed) {
        return false;
    }
    else {
        this$.completed = true;
        action();
        return true;
    }
}

/**
 * Returns a task already cancelled by the supplied token.
 */
export function CapabilityTasks_cancelled(token) {
    const result = CapabilityTasks_Completion$1_$ctor();
    CapabilityTasks_Completion$1__TrySetCanceled_Z211DAE3E(result, token);
    return CapabilityTasks_Completion$1__get_Task(result);
}

function Capabilities_text(argument, value) {
    if (Operators_IsNull(value)) {
        Operators_NullArg(argument);
    }
    if ((value.length === 0) ? true : exists(isControl, value.split(""))) {
        throw new ArgumentException("Expected a nonempty string without control characters.", argument, defaultOf());
    }
    return value;
}

/**
 * Validates a key; image and font query names use invariant lowercase, while ABI names preserve case.
 */
export function Capabilities_key(category, name) {
    let token, matchValue, minor, major;
    if (Operators_IsNull(category)) {
        Operators_NullArg("category");
    }
    const name_1 = Capabilities_text("name", name);
    return new CapabilityKey(category, (category.tag === 2) ? ((exists((c) => {
        if (~~c.charCodeAt(0) > 127) {
            return true;
        }
        else {
            return isWhiteSpace(c);
        }
    }, name_1.split("")) ? (() => {
        throw new ArgumentException("Format names must be ASCII without whitespace.", "name", defaultOf());
    })() : undefined, (token = ((value) => forAll((c_1) => {
        if (isLetterOrDigit(c_1)) {
            return true;
        }
        else {
            return "!#$%&\'*+-.^_`|~".indexOf(c_1) >= 0;
        }
    }, value.split(""))), ((category.tag === 3) ? (!token(name_1) ? (() => {
        throw new ArgumentException("Font formats must be individual tokens.", "name", defaultOf());
    })() : undefined) : ((category.tag === 2) ? ((matchValue = split(name_1, ["/"], undefined, 0), (!equalsWith((x, y) => (x === y), matchValue, defaultOf()) && (matchValue.length === 2)) ? (((minor = item_1(1, matchValue), (major = item_1(0, matchValue), (((major.length > 0) && (minor.length > 0)) && token(major)) && token(minor)))) ? ((item_1(1, matchValue), item_1(0, matchValue), undefined)) : (() => {
        throw new ArgumentException("Image formats require a MIME type without parameters.", "name", defaultOf());
    })()) : (() => {
        throw new ArgumentException("Image formats require a MIME type without parameters.", "name", defaultOf());
    })())) : undefined), name_1.toLowerCase())))) : ((category.tag === 3) ? ((exists((c) => {
        if (~~c.charCodeAt(0) > 127) {
            return true;
        }
        else {
            return isWhiteSpace(c);
        }
    }, name_1.split("")) ? (() => {
        throw new ArgumentException("Format names must be ASCII without whitespace.", "name", defaultOf());
    })() : undefined, (token = ((value) => forAll((c_1) => {
        if (isLetterOrDigit(c_1)) {
            return true;
        }
        else {
            return "!#$%&\'*+-.^_`|~".indexOf(c_1) >= 0;
        }
    }, value.split(""))), ((category.tag === 3) ? (!token(name_1) ? (() => {
        throw new ArgumentException("Font formats must be individual tokens.", "name", defaultOf());
    })() : undefined) : ((category.tag === 2) ? ((matchValue = split(name_1, ["/"], undefined, 0), (!equalsWith((x, y) => (x === y), matchValue, defaultOf()) && (matchValue.length === 2)) ? (((minor = item_1(1, matchValue), (major = item_1(0, matchValue), (((major.length > 0) && (minor.length > 0)) && token(major)) && token(minor)))) ? ((item_1(1, matchValue), item_1(0, matchValue), undefined)) : (() => {
        throw new ArgumentException("Image formats require a MIME type without parameters.", "name", defaultOf());
    })()) : (() => {
        throw new ArgumentException("Image formats require a MIME type without parameters.", "name", defaultOf());
    })())) : undefined), name_1.toLowerCase())))) : name_1));
}

function Capabilities_validatedKey(value) {
    if (Operators_IsNull(value)) {
        Operators_NullArg("key");
    }
    return Capabilities_key(value.Category, value.Name);
}

/**
 * Creates an all-unknown contribution with explicit provider provenance.
 */
export function Capabilities_unknown(key, providerId, mechanism, scope, reason) {
    const fact = new CapabilityFact(CapabilityState.Unknown, reason);
    return new CapabilityObservation(Capabilities_validatedKey(key), fact, fact, fact, fact, fact, undefined, Capabilities_text("providerId", providerId), Capabilities_text("mechanism", mechanism), Capabilities_text("scope", scope));
}

function Capabilities_copyValue(_arg) {
    let value;
    let matchResult, value_2, value_3, values, value_5;
    switch (_arg.tag) {
        case 1: {
            if ((value = _arg.fields[0], Number.isNaN(value) ? true : isInfinity(value))) {
                matchResult = 0;
                value_2 = _arg.fields[0];
            }
            else {
                matchResult = 3;
                value_5 = _arg;
            }
            break;
        }
        case 2: {
            if (Operators_IsNull(_arg.fields[0])) {
                matchResult = 1;
                value_3 = _arg.fields[0];
            }
            else {
                matchResult = 3;
                value_5 = _arg;
            }
            break;
        }
        case 3: {
            matchResult = 2;
            values = _arg.fields[0];
            break;
        }
        default: {
            matchResult = 3;
            value_5 = _arg;
        }
    }
    switch (matchResult) {
        case 0:
            throw new ArgumentException("Capability numbers must be finite.", "value", defaultOf());
        case 1:
            return Operators_NullArg("value");
        case 2: {
            if (Operators_IsNull(values)) {
                Operators_NullArg("values");
            }
            values.forEach((name) => {
                Capabilities_text("values", name);
            });
            return new CapabilityValue(/* Names */ 3, [copy(values)]);
        }
        default:
            return value_5;
    }
}

function Capabilities_copyObservation(expectedKey, expectedId, value) {
    let option;
    if (Operators_IsNull(value)) {
        Operators_NullArg("observation");
    }
    if (!equals(Capabilities_validatedKey(value.Key), expectedKey) ? true : (value.ProviderId !== expectedId)) {
        throw new ArgumentException("The observation key and provider ID must match the request.", "observation", defaultOf());
    }
    const enumerator = getEnumerator([value.Exported, value.Implemented, value.Enabled, value.Permitted, value.Available]);
    try {
        while (enumerator["System.Collections.IEnumerator.MoveNext"]()) {
            const fact = enumerator["System.Collections.Generic.IEnumerator`1.get_Current"]();
            if (Operators_IsNull(fact) ? true : Operators_IsNull(fact.Reason)) {
                throw new ArgumentException("Facts require reasons.", "observation", defaultOf());
            }
        }
    }
    finally {
        disposeSafe(enumerator);
    }
    Capabilities_text("mechanism", value.Mechanism);
    Capabilities_text("scope", value.Scope);
    return new CapabilityObservation(expectedKey, value.Exported, value.Implemented, value.Enabled, value.Permitted, value.Available, (option = value.Value, (option != null) ? Capabilities_copyValue(option) : undefined), value.ProviderId, value.Mechanism, value.Scope);
}

function Capabilities_combine(select, observations) {
    let array_1;
    const known = Array_distinct((array_1 = map((value) => select(value).State, observations), array_1.filter((y) => !equals(CapabilityState.Unknown, y))), {
        Equals: equals,
        GetHashCode: (x_1) => (safeHash(x_1) | 0),
    });
    if (!equalsWith(equals, known, defaultOf()) && (known.length === 1)) {
        return new CapabilityFact(item_1(0, known), "Agreeing observed facts; see contributions.");
    }
    else if (!equalsWith(equals, known, defaultOf()) && (known.length === 0)) {
        return new CapabilityFact(CapabilityState.Unknown, "No known observation; see contributions.");
    }
    else {
        return new CapabilityFact(CapabilityState.Unknown, "Conflicting observations; both facts are retained.");
    }
}

function Capabilities_report(key, observations) {
    const values = Array_distinct(choose((_arg) => _arg.Value, observations), {
        Equals: equals,
        GetHashCode: (x) => (safeHash(x) | 0),
    });
    return new CapabilityReport(key, Capabilities_combine((value) => value.Exported, observations), Capabilities_combine((value_1) => value_1.Implemented, observations), Capabilities_combine((value_2) => value_2.Enabled, observations), Capabilities_combine((value_3) => value_3.Permitted, observations), Capabilities_combine((value_4) => value_4.Available, observations), (values.length === 1) ? Capabilities_copyValue(item_1(0, values)) : undefined, copy(observations));
}

function Capabilities_registerCancellation(token, action) {
    if (!Operators_IsNull(token)) {
        return token.register(action);
    }
    else {
        return {
            Dispose() {
            },
        };
    }
}

/**
 * Creates an empty registry; disposal cancels pending publication even when providers ignore cancellation.
 */
export function Capabilities_create() {
    const gate = {};
    const providers = [];
    const lifetime = createCancellationToken();
    let disposed = false;
    const active = () => {
        if (disposed) {
            throw new CapabilityContextException("CapabilityService is disposed.");
        }
    };
    const snapshot = () => Operators_Lock(gate, () => {
        active();
        return providers.slice();
    });
    return {
        "Ranvier.Dom.CapabilityService.KnownKeys"() {
            return Array_distinct(collect((_arg_2) => _arg_2.KnownKeys, snapshot()), {
                Equals: equals,
                GetHashCode: (x) => (safeHash(x) | 0),
            });
        },
        "Ranvier.Dom.CapabilityService.Register1271AB07"(provider_1) {
            if (Operators_IsNull(provider_1)) {
                Operators_NullArg("provider");
            }
            Capabilities_text("provider.Id", provider_1.Id);
            if (Operators_IsNull(provider_1.KnownKeys)) {
                Operators_NullArg("provider.KnownKeys");
            }
            if (Operators_IsNull(curry2(provider_1.Observe))) {
                Operators_NullArg("provider.Observe");
            }
            const prepared = new CapabilityProvider(provider_1.Id, Array_distinct(map(Capabilities_validatedKey, provider_1.KnownKeys), {
                Equals: equals,
                GetHashCode: (x_1) => (safeHash(x_1) | 0),
            }), provider_1.Observe);
            Operators_Lock(gate, () => {
                active();
                if (exists((current) => (current.Id === prepared.Id), providers)) {
                    throw new ArgumentException("Provider IDs must be unique.", "provider", defaultOf());
                }
                void (providers.push(prepared));
            });
            let removed = false;
            return {
                Dispose() {
                    Operators_Lock(gate, () => {
                        if (!removed) {
                            removed = true;
                            removeInPlace(prepared, providers, {
                                Equals: equals,
                                GetHashCode: (x_2) => (safeHash(x_2) | 0),
                            });
                        }
                    });
                },
            };
        },
        "Ranvier.Dom.CapabilityService.Observe5FB49CD3"(requestKey_1, token_1) {
            let builder$0040_1;
            const requestKey_2 = Capabilities_validatedKey(requestKey_1);
            if (isCancellationRequested(token_1)) {
                return CapabilityTasks_cancelled(token_1);
            }
            else {
                const current_1 = snapshot();
                const result = CapabilityTasks_Completion$1_$ctor();
                const linked = createCancellationToken();
                const requestCancellation = Capabilities_registerCancellation(token_1, () => {
                    cancel(linked);
                });
                const lifetimeCancellation = Capabilities_registerCancellation(lifetime, () => {
                    cancel(linked);
                });
                const cancellation = Capabilities_registerCancellation(linked, () => {
                    Operators_Lock(gate, () => {
                        if (disposed) {
                            CapabilityTasks_Completion$1__TrySetException_229D3F39(result, new CapabilityContextException("CapabilityService is disposed."));
                        }
                        else {
                            CapabilityTasks_Completion$1__TrySetCanceled_Z211DAE3E(result, token_1);
                        }
                    });
                });
                (builder$0040_1 = task(), builder$0040_1.Run(builder$0040_1.Delay(() => builder$0040_1.Combine(builder$0040_1.TryWith(builder$0040_1.Delay(() => {
                    let values, requestKey, token;
                    return builder$0040_1.Bind((values = map((requestKey = requestKey_2, (token = linked, (provider) => {
                        const builder$0040 = task();
                        return builder$0040.Run(builder$0040.Delay(() => builder$0040.TryWith(builder$0040.Delay(() => {
                            const pending = provider.Observe(requestKey, token);
                            return builder$0040.Combine(Operators_IsNull(pending) ? (((() => {
                                throw new InvalidOperationException("Provider returned a null task.");
                            })(), builder$0040.Zero())) : builder$0040.Zero(), builder$0040.Delay(() => builder$0040.Bind(pending, (_arg) => {
                                let option_1;
                                return builder$0040.Return((option_1 = _arg, (option_1 != null) ? Capabilities_copyObservation(requestKey, provider.Id, option_1) : undefined));
                            })));
                        }), (_arg_1) => {
                            let matchResult, error_3;
                            if (_arg_1 instanceof CapabilityContextException) {
                                matchResult = 0;
                            }
                            else if (_arg_1 instanceof OperationCanceledException) {
                                if (isCancellationRequested(token)) {
                                    matchResult = 1;
                                }
                                else {
                                    matchResult = 2;
                                    error_3 = _arg_1;
                                }
                            }
                            else {
                                matchResult = 2;
                                error_3 = _arg_1;
                            }
                            switch (matchResult) {
                                case 0:
                                    return builder$0040.Return((() => {
                                        throw _arg_1;
                                    })());
                                case 1:
                                    return builder$0040.Return((() => {
                                        throw _arg_1;
                                    })());
                                default:
                                    return builder$0040.Return(Capabilities_unknown(requestKey, provider.Id, "provider failure", "provider", error_3.message));
                            }
                        })));
                    })), current_1), Promise.all(values)), (_arg_3) => {
                        const combined = Capabilities_report(requestKey_2, choose((x_3) => x_3, _arg_3));
                        Operators_Lock(gate, () => {
                            if (disposed) {
                                CapabilityTasks_Completion$1__TrySetException_229D3F39(result, new CapabilityContextException("CapabilityService is disposed."));
                            }
                            else if (isCancellationRequested(token_1)) {
                                CapabilityTasks_Completion$1__TrySetCanceled_Z211DAE3E(result, token_1);
                            }
                            else {
                                CapabilityTasks_Completion$1__TrySetResult_2B595(result, combined);
                            }
                        });
                        return builder$0040_1.Zero();
                    });
                }), (_arg_4) => {
                    CapabilityTasks_Completion$1__TrySetException_229D3F39(result, _arg_4);
                    return builder$0040_1.Zero();
                }), builder$0040_1.Delay(() => {
                    disposeSafe(cancellation);
                    disposeSafe(requestCancellation);
                    disposeSafe(lifetimeCancellation);
                    return builder$0040_1.Zero();
                })))));
                return CapabilityTasks_Completion$1__get_Task(result);
            }
        },
        Dispose() {
            if (Operators_Lock(gate, () => {
                if (disposed) {
                    return false;
                }
                else {
                    disposed = true;
                    clear(providers);
                    return true;
                }
            })) {
                cancel(lifetime);
            }
        },
    };
}

