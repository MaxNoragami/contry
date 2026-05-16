export declare const APP_URLS: {
    readonly api: {
        readonly developmentTarget: "http://localhost:5087";
        readonly production: "https://api.contry.app";
    };
    readonly lucideStaticBase: "https://unpkg.com/lucide-static@latest";
};
export declare const API_PATHS: {
    readonly auth: {
        readonly register: "/users";
        readonly currentUser: "/users/me";
        readonly login: "/sessions";
        readonly currentSession: "/sessions/current";
        readonly refresh: "/tokens/refresh";
        readonly xsrf: "/xsrf";
    };
    readonly ranked: {
        readonly challengeCurrent: "/ranked/challenges/current";
        readonly sessionCurrent: "/ranked/sessions/current";
        readonly sessionGiveUp: "/ranked/sessions/current/give-up";
        readonly guesses: "/ranked/guesses";
        readonly statsMe: "/ranked/stats/me";
    };
    readonly leaderboards: {
        readonly ranked: "/leaderboards/ranked";
    };
    readonly datasets: {
        readonly manifest: "/datasets/manifest.json";
        readonly baseCountries: "/datasets/base/countries.csv";
    };
};
export declare const STORAGE_KEYS: {
    readonly gameMode: "contry.game_mode";
};
export declare const APP_LIMITS: {
    readonly activeClueCount: 5;
    readonly suggestionCount: 4;
    readonly leaderboardPageSize: 7;
    readonly iconPickerResultCount: 50;
    readonly uploadMissingExampleCount: 3;
    readonly toastVisibleCount: 3;
};
export declare const APP_TIMINGS: {
    readonly modalResetMs: 300;
    readonly guessShakeMs: 500;
    readonly submitPreviewMs: 250;
    readonly keyboardRefocusMs: 10;
    readonly toastDurationMs: 3000;
};
export declare const DEFAULT_CLUE_IDS: readonly ["hemisphere", "continent", "temperature_avg_c", "population", "coordinates"];
export declare function getLucideIconUrl(iconName: string): string;
export declare function getLucideTagsUrl(): string;
