From Stdlib Require Import List ZArith.
From RanvierView.Pure Require Import Layout Hydration Lifetime Scheduling.
From RanvierView Require Import Extract.
From RanvierView.Conformance Require Import Expected.
Import ListNotations.

(** Each [check_Sxx] verifies by [vm_compute] that the Rocq pure model matches the
    concrete output [exp_Sxx] recorded by [proofs/conformance/Scenarios.fsx] from
    running the real F# implementation. *)

Example check_S01 :
  observe_layout [11; 12; 13; 14]
    [LCmdRegisterExit 12; LCmdReconcile [14; 11; 13]; LCmdFinishExit 12] = exp_S01.
Proof. vm_compute. reflexivity. Qed.

Example check_S02 :
  observe_layout [21; 22]
    [LCmdRegisterExit 21; LCmdReconcile []; LCmdReflowBefore; LCmdReflowBefore;
     LCmdReflowPost 0; LCmdReflowPost 1] = exp_S02.
Proof. vm_compute. reflexivity. Qed.

Example check_S03 :
  logical_next
    (flatten_tree (SRegion 1 [SNode 10; SRegion 2 [SNode 20; SRegion 3 []]; SNode 30]) ++
     [TokNode 99]) = exp_S03.
Proof. vm_compute. reflexivity. Qed.

Example check_S04 :
  observe_cursor_rollback 100%Z
    [HAdd 10%Z; HScope [HAdd 5%Z; HAdd (-3)%Z]; HAdd 20%Z] false = exp_S04.
Proof. vm_compute. reflexivity. Qed.

Example check_S05 :
  observe_cursor_rollback 50%Z
    [HAdd 7%Z; HScope [HAdd 3%Z]] true = exp_S05.
Proof. vm_compute. reflexivity. Qed.

Example check_S06 :
  observe_boundary true
    [BCmdExpect; BCmdExpect; BCmdEndSetup;
     BCmdStep 0 OutNotReady; BCmdStep 0 OutValue; BCmdStep 1 OutValue;
     BCmdReveal; BCmdStep 0 OutError; BCmdRetry 0; BCmdRetry 1] = exp_S06.
Proof. vm_compute. reflexivity. Qed.

Example check_S07 :
  observe_placement false
    [PCmdEnter; PCmdAdd 101; PCmdEnter; PCmdAdd 201; PCmdLeave;
     PCmdAdd 102; PCmdReleaseChild 901; PCmdPlace] = exp_S07.
Proof. vm_compute. reflexivity. Qed.

Example check_S08 :
  dispose_lifetime_tree
    (LTNode 1 [10; 11] [LTNode 2 [20] [LTNode 3 [30] []]]) = exp_S08.
Proof. vm_compute. reflexivity. Qed.

Example check_S09 :
  observe_queue
    [QCmdReserve 1 false; QCmdReserve 2 true; QCmdReserve 1 true;
     QCmdEnqueue 0 10; QCmdEnqueue 0 11; QCmdEnqueue 1 20; QCmdEnqueue 2 30;
     QCmdFlush FailFastCancel [11]; QCmdRunTicket 0 CollectAndContinue []] = exp_S09.
Proof. vm_compute. reflexivity. Qed.

Example check_S10 :
  (observe_owq [OWQSetWake true; OWQEnqueue 1 2; OWQEnqueue 2 2; OWQEnqueue 3 2;
                OWQDequeue; OWQStop],
   observe_fence [CFTryComplete PollNotReady; CFReset; CFTryComplete PollGpuFailure;
                  CFReset; CFTryComplete PollReady]) = exp_S10.
Proof. vm_compute. reflexivity. Qed.

Example check_S11 :
  observe_layout [31; 32]
    [LCmdReflowBefore; LCmdReflowCancel 0; LCmdReflowBefore; LCmdReflowPost 1] = exp_S11.
Proof. vm_compute. reflexivity. Qed.

Example check_S12 :
  observe_queue
    [QCmdReserve 1 false; QCmdReserve 2 true; QCmdReserve 2 true;
     QCmdEnqueue 0 10; QCmdEnqueue 1 20;
     QCmdFlushReenq FailFastCancel [10] [(10, 1, 30); (10, 2, 40)];
     QCmdFlush FailFastCancel []] = exp_S12.
Proof. vm_compute. reflexivity. Qed.
