export type ViewType = 'main' | 'clues' | 'add-clue' | 'edit-clue' | 'view-clue' | 'explore-clues' | 'icon-picker' | 'dataset-editor' | 'admin-panel' | 'admin-ranked-round' | 'admin-reset-leaderboard-warning' | 'clear-cache-warning';

export type NavDirection = 'forward' | 'back';

export type DraftClueData = {
  mode: 'create' | 'edit' | 'view';
  originalId: string | null;
  baselineSnapshot?: string | null;
  id: string;
  label: string;
  description: string;
  type: 'numeric' | 'categorical';
  comparator: 'higher_lower' | 'exact';
  unitSymbol: string;
  icon: string;
  categories: string[];
  data: { country_id: string; value: any }[];
  remoteId?: string | null;
  ownerId?: string | null;
  ownerUsername?: string | null;
  visibility?: 'public' | 'private' | null;
  readOnly?: boolean;
};
