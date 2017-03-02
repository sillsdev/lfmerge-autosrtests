# Automated LfMerge S/R Tests

NOTE: This project requires Mono 4.x, i.e. the package `mono4-sil` has to be installed!

## Restore existing Chorus repos

The mocked LanguageDepot/Chorus repo is stored in the source tree as a series of
patch files in the `data` directory. The repo will be restored during a test run.
However, for adding or modifying tests it might be necessary to manually restore the
repo. This can be achieved by creating a Mercurial repo and then applying the patches:

	mkdir -p workdir/testproj
	hg init .
	hg import ../../data/r0.patch
	hg import ../../data/7000068/r1.patch
	etc.

## Export Mongo database

	mongoexport --db scriptureforge --collection projects --query '{ "projectName" : $projectname}' > $projectname.json
	for col in activity lexicon optionlists; do
		mongoexport --db $projectname --collection $col > ${projectname}.${col}.json
	done

## Restore Mongo database

	mongoimport --db scriptureforge --collection projects --file $dbname.json
	for col in activity lexicon optionlists; do
		mongoimport --db $dbname --drop --collection $col --file ${dbname}.${col}.json
	done
