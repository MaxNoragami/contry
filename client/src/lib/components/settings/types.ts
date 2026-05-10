export type ViewType = 'main' | 'clues' | 'add-clue' | 'edit-clue' | 'icon-picker' | 'dataset-editor' | 'clear-cache-warning';

export type NavDirection = 'forward' | 'back';

export type DraftClueData = {
  mode: 'create' | 'edit';
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
};
