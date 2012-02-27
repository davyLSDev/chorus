using System.Collections.Generic;
using System.Xml;
using Chorus.FileTypeHanders.xml;

namespace Chorus.merge.xml.generic
{
	/// <summary>
	/// Merge the attributes of an element.
	/// </summary>
	internal static class MergeXmlAttributesService
	{
		internal static void MergeAttributes(XmlMerger merger, ref XmlNode ours, XmlNode theirs, XmlNode ancestor)
		{
			var extantNode = ours ?? theirs ?? ancestor;

			var skipProcessingInOurs = new HashSet<string>();
			// Deletions from ancestor, no matter who did it.
			foreach (var ancestorAttr in XmlUtilities.GetAttrs(ancestor))
			{
				var ourAttr = XmlUtilities.GetAttributeOrNull(ours, ancestorAttr.Name);
				var theirAttr = XmlUtilities.GetAttributeOrNull(theirs, ancestorAttr.Name);
				if (theirAttr == null)
				{
					if (ourAttr == null)
					{
						// Both deleted.
						merger.EventListener.ChangeOccurred(new XmlAttributeBothDeletedReport(merger.MergeSituation.PathToFileInRepository, ancestorAttr));
						ancestor.Attributes.Remove(ancestorAttr);
						continue;
					}
					if (ourAttr.Value != ancestorAttr.Value)
					{
						// They deleted, but we changed, so we win under the principle of
						// least data loss (an attribute can be a huge text element).
						merger.ConflictOccurred(new EditedVsRemovedAttributeConflict(ourAttr.Name, ourAttr.Value, null, ancestorAttr.Value, merger.MergeSituation, merger.MergeSituation.AlphaUserId));
						continue;
					}
					// They deleted. We did zip.
					merger.EventListener.ChangeOccurred(new XmlAttributeDeletedReport(merger.MergeSituation.PathToFileInRepository, ancestorAttr));
					ancestor.Attributes.Remove(ancestorAttr);
					ours.Attributes.Remove(ourAttr);
					continue;
				}
				if (ourAttr == null)
				{
					if (ancestorAttr.Value != theirAttr.Value)
					{
						// We deleted it, but at the same time, they changed it. So just add theirs in, under the principle of
						// least data loss (an attribute can be a huge text element)
						skipProcessingInOurs.Add(theirAttr.Name); // Make sure we don't process it again in 'ours loop, below.
						var importedAttribute = (XmlAttribute)ours.OwnerDocument.ImportNode(theirAttr.CloneNode(true), true);
						ours.Attributes.Append(importedAttribute);
						merger.ConflictOccurred(new RemovedVsEditedAttributeConflict(theirAttr.Name, null, theirAttr.Value, ancestorAttr.Value, merger.MergeSituation,
							merger.MergeSituation.BetaUserId));
						continue;
					}
					// We deleted it. They did nothing.
					merger.EventListener.ChangeOccurred(new XmlAttributeDeletedReport(merger.MergeSituation.PathToFileInRepository, ancestorAttr));
					ancestor.Attributes.Remove(ancestorAttr);
					theirs.Attributes.Remove(theirAttr);
				}
			}

			foreach (var theirAttr in XmlUtilities.GetAttrs(theirs))
			{
				// Will never return null, since it will use the default one, if it can't find a better one.
				var mergeStrategy = merger.MergeStrategies.GetElementStrategy(extantNode);
				var ourAttr = XmlUtilities.GetAttributeOrNull(ours, theirAttr.Name);
				var ancestorAttr = XmlUtilities.GetAttributeOrNull(ancestor, theirAttr.Name);

				if (ourAttr == null)
				{
					if (ancestorAttr == null)
					{
						skipProcessingInOurs.Add(theirAttr.Name); // Make sure we don't process it again in 'ours loop, below.
						var importedAttribute = (XmlAttribute)ours.OwnerDocument.ImportNode(theirAttr.CloneNode(true), true);
						ours.Attributes.Append(importedAttribute);
						merger.EventListener.ChangeOccurred(new XmlAttributeAddedReport(merger.MergeSituation.PathToFileInRepository, theirAttr));
					}
					// NB: Deletes are all handles above in first loop.
					//else if (ancestorAttr.Value == theirAttr.Value)
					//{
					//    continue; // we deleted it, they didn't touch it
					//}
					//else
					//{
					//    // We deleted it, but at the same time, they changed it. So just add theirs in, under the principle of
					//    // least data loss (an attribute can be a huge text element)
					//    var importedAttribute = (XmlAttribute)ours.OwnerDocument.ImportNode(theirAttr, true);
					//    ours.Attributes.Append(importedAttribute);

					//    EventListener.ConflictOccurred(new RemovedVsEditedAttributeConflict(theirAttr.Name, null, theirAttr.Value, ancestorAttr.Value, MergeSituation,
					//        MergeSituation.BetaUserId));
					//    continue;
					//}
				}
				else if (ancestorAttr == null) // Both introduced this attribute
				{
					if (ourAttr.Value == theirAttr.Value)
					{
						merger.EventListener.ChangeOccurred(new XmlAttributeBothAddedReport(merger.MergeSituation.PathToFileInRepository, ourAttr));
						continue;
					}
					else
					{
						// Both added, but not the same.
						if (!mergeStrategy.AttributesToIgnoreForMerging.Contains(ourAttr.Name))
						{
							if (merger.MergeSituation.ConflictHandlingMode == MergeOrder.ConflictHandlingModeChoices.WeWin)
							{
								merger.ConflictOccurred(new BothAddedAttributeConflict(theirAttr.Name, ourAttr.Value, theirAttr.Value, merger.MergeSituation,
									merger.MergeSituation.AlphaUserId));
							}
							else
							{
								ourAttr.Value = theirAttr.Value;
								merger.ConflictOccurred(new BothAddedAttributeConflict(theirAttr.Name, ourAttr.Value, theirAttr.Value, merger.MergeSituation,
									merger.MergeSituation.BetaUserId));
							}
						}
					}
				}
				else if (ancestorAttr.Value == ourAttr.Value)
				{
					if (ourAttr.Value == theirAttr.Value)
					{
						continue; // Nothing to do.
					}
					else
					{
						// They changed.
						if (!mergeStrategy.AttributesToIgnoreForMerging.Contains(ourAttr.Name))
						{
							skipProcessingInOurs.Add(theirAttr.Name);
							merger.EventListener.ChangeOccurred(new XmlAttributeChangedReport(merger.MergeSituation.PathToFileInRepository, theirAttr));
							ourAttr.Value = theirAttr.Value;
						}
						continue;
					}
				}
				else if (ourAttr.Value == theirAttr.Value)
				{
					// Both changed to same value
					if (skipProcessingInOurs.Contains(theirAttr.Name))
						continue;
					merger.EventListener.ChangeOccurred(new XmlAttributeBothMadeSameChangeReport(merger.MergeSituation.PathToFileInRepository, ourAttr));
					continue;
				}
				else if (ancestorAttr.Value == theirAttr.Value)
				{
					// We changed the value. They did nothing.
					if (!mergeStrategy.AttributesToIgnoreForMerging.Contains(ourAttr.Name))
					{
						merger.EventListener.ChangeOccurred(new XmlAttributeChangedReport(merger.MergeSituation.PathToFileInRepository, ourAttr));
						continue;
					}
				}
				else
				{
					//for unit test see Merge_RealConflictPlusModDateConflict_ModDateNotReportedAsConflict()
					if (!mergeStrategy.AttributesToIgnoreForMerging.Contains(ourAttr.Name))
					{
						if (merger.MergeSituation.ConflictHandlingMode == MergeOrder.ConflictHandlingModeChoices.WeWin)
						{
							merger.ConflictOccurred(new BothEditedAttributeConflict(theirAttr.Name, ourAttr.Value,
																							theirAttr.Value,
																							ancestorAttr.Value,
																							merger.MergeSituation,
																							merger.MergeSituation.AlphaUserId));
						}
						else
						{
							ourAttr.Value = theirAttr.Value;
							merger.ConflictOccurred(new BothEditedAttributeConflict(theirAttr.Name, ourAttr.Value,
																							theirAttr.Value,
																							ancestorAttr.Value,
																							merger.MergeSituation,
																							merger.MergeSituation.BetaUserId));
						}
					}
				}
			}

			foreach (var ourAttr in XmlUtilities.GetAttrs(ours))
			{
				if (skipProcessingInOurs.Contains(ourAttr.Name))
					continue;

				var theirAttr = XmlUtilities.GetAttributeOrNull(theirs, ourAttr.Name);
				var ancestorAttr = XmlUtilities.GetAttributeOrNull(ancestor, ourAttr.Name);

				if (ancestorAttr == null)
				{
					if (theirAttr == null)
					{
						// We added it.
						merger.EventListener.ChangeOccurred(new XmlAttributeAddedReport(merger.MergeSituation.PathToFileInRepository, ourAttr));
						continue;
					}
					// They also added, and it may, or may not, be the same.
					continue;
				}
				if (theirAttr == null)
				{
					if (ourAttr.Value == ancestorAttr.Value) //we didn't change it, they deleted it
					{
						merger.EventListener.ChangeOccurred(new XmlAttributeDeletedReport(merger.MergeSituation.PathToFileInRepository, ourAttr));
						ours.Attributes.Remove(ourAttr);
					}
					// NB: Deletes are all handles above in first loop.
					//else
					//{
					//    // We changed it, and they deleted it, so stay with ours and add conflict report.
					//    EventListener.ConflictOccurred(new EditedVsRemovedAttributeConflict(ourAttr.Name, ourAttr.Value, null, ancestorAttr.Value, MergeSituation, MergeSituation.AlphaUserId));
					//}
				}
			}
		}
	}
}