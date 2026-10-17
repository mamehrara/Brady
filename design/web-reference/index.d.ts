import type * as React from 'react';
export type ReminderKind = 'posture' | 'move' | 'eyes' | 'water' | 'stretch';
export type IconName = ReminderKind | 'snooze' | 'check' | 'close';
export interface ToastProps { kind?: ReminderKind; title: React.ReactNode; children?: React.ReactNode; meta?: React.ReactNode; mood?: 'awake' | 'sleepy' | 'cheer'; doneLabel?: string; snoozeLabel?: string; onDone?: () => void; onSnooze?: () => void; onSkip?: () => void; onClose?: (() => void) | false; actions?: false }
export declare function Toast(props: ToastProps): React.ReactElement;
export interface ButtonProps extends React.ButtonHTMLAttributes<HTMLButtonElement> { variant?: 'primary' | 'glass' | 'quiet'; size?: 'md' | 'sm'; icon?: IconName }
export declare function Button(props: ButtonProps): React.ReactElement;
export interface ChipProps { kind?: ReminderKind; children?: React.ReactNode; className?: string }
export declare function Chip(props: ChipProps): React.ReactElement;
export interface CountdownProps { remaining?: number; total?: number; kind?: ReminderKind; label?: React.ReactNode; className?: string }
export declare function Countdown(props: CountdownProps): React.ReactElement;
export interface SwitchProps { checked?: boolean; label: React.ReactNode; description?: React.ReactNode; disabled?: boolean; onChange?: (checked: boolean) => void; className?: string }
export declare function Switch(props: SwitchProps): React.ReactElement;
export interface MascotProps { pose?: 'hang' | 'head'; mood?: 'awake' | 'sleepy' | 'cheer'; size?: number; label?: string; className?: string }
export declare function Mascot(props: MascotProps): React.ReactElement;
export interface IconProps { name: IconName; size?: number; label?: string; className?: string }
export declare function Icon(props: IconProps): React.ReactElement;
declare global { interface Window { Brady: { Toast: typeof Toast; Button: typeof Button; Chip: typeof Chip; Countdown: typeof Countdown; Switch: typeof Switch; Mascot: typeof Mascot; Icon: typeof Icon } } }
