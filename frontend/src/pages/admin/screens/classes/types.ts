export const DEFAULT_ROOMS = [
  'Phòng Yoga',
  'Studio A',
  'Studio B',
  'Phòng Cycling',
  'Phòng Boxing',
  'Phòng Pilates',
  'Khu chức năng',
];

export type BranchOption = {
  id: string;
  name: string;
  rooms: string[];
};

export type TrainerOption = {
  id: string;
  fullName: string;
  branchName: string;
};
