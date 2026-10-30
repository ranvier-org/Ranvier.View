
import { task } from "./fable_modules/fable-library-js.5.24.0/TaskBuilder.js";
import { defaultOf, equals, Exception } from "./fable_modules/fable-library-js.5.24.0/Util.js";
import { forDocument } from "./Users/shaya/.codex/worktrees/native-capabilities-core/Ranvier/src/Fable.Ranvier.Dom/BrowserCapabilities.js";
import { Capabilities_unknown, CapabilityContextException, CapabilityProvider, CapabilityValue, CapabilityState, CapabilityCategory, Capabilities_key } from "./Users/shaya/.codex/worktrees/native-capabilities-core/Ranvier/src/Ranvier.Dom/Capabilities.js";
import { cancel, awaitPromise, startAsTask, createCancellationToken } from "./fable_modules/fable-library-js.5.24.0/Async.js";
import { TaskCompletionSource, fromResult } from "./fable_modules/fable-library-js.5.24.0/Task.js";
import { unwrap } from "./fable_modules/fable-library-js.5.24.0/Option.js";
import { OperationCanceledException } from "./fable_modules/fable-library-js.5.24.0/AsyncBuilder.js";

export function run() {
    const builder$0040 = task();
    return builder$0040.Run(builder$0040.Delay(() => {
        let count = 0;
        const expect = (condition, message) => {
            count = ((count + 1) | 0);
            if (!condition) {
                throw new Exception(message);
            }
        };
        return builder$0040.Using(forDocument(document), (_arg) => {
            const service = _arg;
            expect(service["Ranvier.Dom.CapabilityService.KnownKeys"]().length > 30, "Browser query catalogue");
            const unknownKey = Capabilities_key(CapabilityCategory.PlatformFacility, "unrecognized-future");
            return builder$0040.Bind(service["Ranvier.Dom.CapabilityService.Observe5FB49CD3"](unknownKey, createCancellationToken()), (_arg_1) => {
                const unknown = _arg_1;
                expect(equals(unknown.Exported.State, CapabilityState.Unknown) && equals(unknown.Implemented.State, CapabilityState.Unknown), "Unknown key remains unknown");
                return builder$0040.Bind(service["Ranvier.Dom.CapabilityService.Observe5FB49CD3"](Capabilities_key(CapabilityCategory.PlatformFacility, "webgl"), createCancellationToken()), (_arg_2) => {
                    const webgl = _arg_2;
                    expect(equals(webgl.Implemented.State, CapabilityState.Unknown) && equals(webgl.Available.State, CapabilityState.Unknown), "Constructor presence does not prove GPU behavior or readiness");
                    return builder$0040.Bind(service["Ranvier.Dom.CapabilityService.Observe5FB49CD3"](Capabilities_key(CapabilityCategory.FontFormat, "woff2"), createCancellationToken()), (_arg_3) => {
                        const font = _arg_3;
                        expect(equals(font.Value, new CapabilityValue(/* Boolean */ 0, [document.defaultView.CSS.supports('font-format('+"woff2"+')')])), "Actual CSS font-format query parity");
                        expect(equals(font.Available.State, CapabilityState.Unknown), "Font query does not certify actual loading");
                        return builder$0040.Bind(service["Ranvier.Dom.CapabilityService.Observe5FB49CD3"](Capabilities_key(CapabilityCategory.ImageFormat, "image/png"), createCancellationToken()), (_arg_4) => {
                            const image = _arg_4;
                            return builder$0040.Combine((typeof document.defaultView.ImageDecoder?.isTypeSupported === 'function') ? builder$0040.Bind(startAsTask(awaitPromise(document.defaultView.ImageDecoder.isTypeSupported("image/png"))), (_arg_5) => {
                                expect(equals(image.Value, new CapabilityValue(/* Boolean */ 0, [_arg_5])), "Actual realm ImageDecoder query parity");
                                return builder$0040.Zero();
                            }) : ((expect(equals(image.Implemented.State, CapabilityState.Unknown), "Missing ImageDecoder query leaves image behavior unknown"), builder$0040.Zero())), builder$0040.Delay(() => {
                                expect(equals(image.Available.State, CapabilityState.Unknown), "Image query does not certify arbitrary decoding/rendering");
                                const comparisons = [];
                                return builder$0040.Combine(builder$0040.For([["image/png", "data:image/png;base64,iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAwMCAO+jRZkAAAAASUVORK5CYII="], ["image/svg+xml", "data:image/svg+xml,%3Csvg%20xmlns=\'http://www.w3.org/2000/svg\'%20width=\'1\'%20height=\'1\'%3E%3Crect%20width=\'1\'%20height=\'1\'%20fill=\'red\'/%3E%3C/svg%3E"]], (_arg_6) => {
                                    const mime = _arg_6[0];
                                    return builder$0040.Bind(startAsTask(awaitPromise(new Promise(resolve => { const i=new document.defaultView.Image(); i.onload=()=>resolve(i.naturalWidth>0); i.onerror=()=>resolve(false); i.src=_arg_6[1]; }))), (_arg_7) => {
                                        const html = _arg_7;
                                        expect(html, "Actual HTML image load succeeds: " + mime);
                                        return builder$0040.Bind(service["Ranvier.Dom.CapabilityService.Observe5FB49CD3"](Capabilities_key(CapabilityCategory.ImageFormat, mime), createCancellationToken()), (_arg_8) => {
                                            let builder$0040_1;
                                            const queried = _arg_8;
                                            expect(equals(queried.Available.State, CapabilityState.Unknown), "ImageDecoder query remains separate from renderer availability");
                                            return builder$0040.Bind((typeof document.defaultView.ImageDecoder?.isTypeSupported === 'function') ? ((builder$0040_1 = task(), builder$0040_1.Run(builder$0040_1.Delay(() => builder$0040_1.Bind(startAsTask(awaitPromise(document.defaultView.ImageDecoder.isTypeSupported(mime))), (_arg_9) => builder$0040_1.Return(_arg_9)))))) : fromResult(undefined), (_arg_10) => {
                                                let option;
                                                const raw = _arg_10;
                                                expect(equals(queried.Value, (option = raw, (option != null) ? (new CapabilityValue(/* Boolean */ 0, [option])) : undefined)), "Facade preserves precise ImageDecoder query result");
                                                void (comparisons.push({
                                                    htmlImageLoaded: html,
                                                    imageDecoderSupported: unwrap(raw),
                                                    mime: mime,
                                                }));
                                                return builder$0040.Zero();
                                            });
                                        });
                                    });
                                }), builder$0040.Delay(() => builder$0040.Using(createCancellationToken(), (_arg_11) => {
                                    const cancellation = _arg_11;
                                    cancel(cancellation);
                                    return builder$0040.Combine(builder$0040.TryWith(builder$0040.Delay(() => builder$0040.Bind(service["Ranvier.Dom.CapabilityService.Observe5FB49CD3"](unknownKey, cancellation), (_arg_12) => {
                                        throw new Exception("Expected pre-cancellation");
                                        return builder$0040.Zero();
                                    })), (_arg_13) => {
                                        if (_arg_13 instanceof OperationCanceledException) {
                                            count = ((count + 1) | 0);
                                            return builder$0040.Zero();
                                        }
                                        else {
                                            throw _arg_13;
                                            return defaultOf();
                                        }
                                    }), builder$0040.Delay(() => {
                                        const frame = (() => { const f=document.createElement('iframe'); document.body.append(f); return f; })();
                                        return builder$0040.Using(forDocument(frame.contentDocument), (_arg_14) => {
                                            const other = _arg_14;
                                            const pending = new TaskCompletionSource();
                                            return builder$0040.Using(other["Ranvier.Dom.CapabilityService.Register1271AB07"](new CapabilityProvider("pending", [unknownKey], (_arg_15, _arg_16) => pending.get_Task())), (_arg_17) => {
                                                const beforeHide = other["Ranvier.Dom.CapabilityService.Observe5FB49CD3"](unknownKey, createCancellationToken());
                                                frame.contentWindow.dispatchEvent(new frame.contentWindow.Event('pagehide'));
                                                return builder$0040.Combine(builder$0040.TryWith(builder$0040.Delay(() => builder$0040.Bind(beforeHide, (_arg_18) => {
                                                    throw new Exception("Expected pagehide disposal");
                                                    return builder$0040.Zero();
                                                })), (_arg_19) => {
                                                    if (_arg_19 instanceof CapabilityContextException) {
                                                        count = ((count + 1) | 0);
                                                        return builder$0040.Zero();
                                                    }
                                                    else {
                                                        throw _arg_19;
                                                        return defaultOf();
                                                    }
                                                }), builder$0040.Delay(() => {
                                                    pending.SetResult(Capabilities_unknown(unknownKey, "pending", "fixture", "iframe", "late"));
                                                    frame.remove();
                                                    window.__capabilityResult = {
                                                        error: "",
                                                        imageComparisons: comparisons.slice(),
                                                        passed: count,
                                                    };
                                                    return builder$0040.Zero();
                                                }));
                                            });
                                        });
                                    }));
                                })));
                            }));
                        });
                    });
                });
            });
        });
    }));
}

(() => {
    const builder$0040 = task();
    return builder$0040.Run(builder$0040.Delay(() => builder$0040.TryWith(builder$0040.Delay(() => builder$0040.Bind(run(), () => builder$0040.Zero())), (_arg_1) => {
        window.__capabilityResult = {
            error: JSON.stringify({fields:_arg_1,name:_arg_1?.name,message:_arg_1?.message,stack:_arg_1?.stack,text:String(_arg_1)}),
            passed: 0,
        };
        return builder$0040.Zero();
    })));
})();

